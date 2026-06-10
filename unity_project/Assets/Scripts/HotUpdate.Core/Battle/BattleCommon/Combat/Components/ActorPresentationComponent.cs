using System;
using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;
using Framework;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public class CombatShaderHitModifyRequest
    {
        public int Tag;
        public Color HitColorValue = Color.white;
        public float HitParamValue;
        public long Timestamp;
    }

    public class CombatShaderController : IDisposable
    {
        private const float MaxHitFlashValue = 0.6f;

        private static readonly int HitParamsParam = Shader.PropertyToID("_HitParams");
        private static readonly int FadeoutParam = Shader.PropertyToID("_Clip");
        private static readonly int DarkParam = Shader.PropertyToID("_Dark");
        private static readonly int BaseColorParam = Shader.PropertyToID("_BaseColor");
        private static readonly int HitColorParam = Shader.PropertyToID("_HitColor");
        private static readonly Color DarkColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color PoisonBaseColor = new Color(183 / 255f, 207 / 255f, 61 / 255f);

        private readonly Renderer[] _renderers;
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
        private readonly Dictionary<Renderer, float> _darkRecord = new Dictionary<Renderer, float>();
        private readonly Dictionary<Renderer, Color> _baseColorRecord = new Dictionary<Renderer, Color>();
        private readonly List<CombatShaderHitModifyRequest> _colorRequests = new List<CombatShaderHitModifyRequest>();

        private readonly float _flashDuration;
        private readonly float _halfDuration;
        private bool _isFlashing;
        private bool _isDark;
        private bool _isPoisoned;
        private float _timer;
        private float _hitFlashValue;
        private float _requestedHitParamValue;
        private Color _requestedHitColor = Color.white;
        private long _nextTimestamp;

        public CombatShaderController(Renderer[] renderers, float flashDuration = 0.2f)
        {
            _renderers = renderers ?? Array.Empty<Renderer>();
            _flashDuration = Mathf.Max(0.01f, flashDuration);
            _halfDuration = _flashDuration * 0.5f;
        }

        public void PlayHitFlash()
        {
            _isFlashing = true;
            _timer = 0f;
        }

        public void StopHitFlash()
        {
            if (!_isFlashing)
                return;

            SetHitFlashParam(0f);
            _isFlashing = false;
            _timer = 0f;
        }

        public void TickHitFlash(float deltaTime)
        {
            if (!_isFlashing)
                return;

            _timer += Mathf.Max(0f, deltaTime);

            float value;
            if (_timer < _halfDuration)
            {
                value = _timer / _halfDuration;
            }
            else if (_timer < _flashDuration)
            {
                value = 1f - (_timer - _halfDuration) / _halfDuration;
            }
            else
            {
                value = 0f;
                _isFlashing = false;
                _timer = 0f;
            }

            SetHitFlashParam(value);
        }

        public void SetFadeOutAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            ApplyToRendererBlocks((block, renderer) =>
            {
                if (!renderer.sharedMaterial.HasProperty(FadeoutParam))
                    return false;

                var vec = block.GetVector(FadeoutParam);
                vec.x = alpha;
                vec.y = 0f;
                block.SetVector(FadeoutParam, vec);
                return true;
            });
        }

        public void SetDarkened(bool isDark = true)
        {
            _isDark = isDark;
            ApplyToRendererBlocks((block, renderer) =>
            {
                var material = renderer.sharedMaterial;
                if (material.HasProperty(DarkParam))
                {
                    if (!_darkRecord.ContainsKey(renderer))
                    {
                        _darkRecord[renderer] = material.GetFloat(DarkParam);
                    }

                    block.SetFloat(DarkParam, isDark ? DarkColor.r : _darkRecord[renderer]);
                    return true;
                }

                if (material.HasProperty(BaseColorParam))
                {
                    if (!_baseColorRecord.ContainsKey(renderer))
                    {
                        _baseColorRecord[renderer] = material.GetColor(BaseColorParam);
                    }

                    block.SetColor(BaseColorParam, ResolveBaseTintColor(renderer, true));
                    return true;
                }

                return false;
            });
        }

        public void SetPoisonTint(bool isPoisoned)
        {
            _isPoisoned = isPoisoned;
            ApplyToRendererBlocks((block, renderer) =>
            {
                var material = renderer.sharedMaterial;
                if (!material.HasProperty(BaseColorParam))
                    return false;

                if (!_baseColorRecord.ContainsKey(renderer))
                {
                    _baseColorRecord[renderer] = material.GetColor(BaseColorParam);
                }

                block.SetColor(BaseColorParam, ResolveBaseTintColor(renderer, !material.HasProperty(DarkParam)));
                return true;
            });
        }

        public void AddHitTintRequest(CombatShaderHitModifyRequest request)
        {
            if (request == null)
                return;

            RemoveHitTintRequestsByTag(request.Tag);
            request.Timestamp = _nextTimestamp++;
            _colorRequests.Add(request);
            RefreshHitTintRequestState();
        }

        public void RemoveHitTintRequest(int tag)
        {
            RemoveHitTintRequestsByTag(tag);
            RefreshHitTintRequestState();
        }

        public void Dispose()
        {
            StopHitFlash();
            SetPoisonTint(false);
            SetDarkened(false);
            _darkRecord.Clear();
            _baseColorRecord.Clear();
            _colorRequests.Clear();
        }

        private void RemoveHitTintRequestsByTag(int tag)
        {
            for (int i = _colorRequests.Count - 1; i >= 0; i--)
            {
                if (_colorRequests[i].Tag == tag)
                {
                    _colorRequests.RemoveAt(i);
                }
            }
        }

        private void SetHitFlashParam(float value)
        {
            _hitFlashValue = Mathf.Clamp(value, 0f, MaxHitFlashValue);
            ApplyHitFeedbackState();
        }

        private void ApplyHitFeedbackState()
        {
            float finalParam = Mathf.Max(_requestedHitParamValue, _hitFlashValue);
            ApplyToRendererBlocks((block, renderer) =>
            {
                var material = renderer.sharedMaterial;
                bool changed = false;

                if (material.HasProperty(HitColorParam))
                {
                    block.SetColor(HitColorParam, _requestedHitColor);
                    changed = true;
                }

                if (material.HasProperty(HitParamsParam))
                {
                    block.SetFloat(HitParamsParam, finalParam);
                    changed = true;
                }

                return changed;
            });
        }

        private void RefreshHitTintRequestState()
        {
            _requestedHitColor = Color.white;
            _requestedHitParamValue = 0f;

            if (_colorRequests.Count > 0)
            {
                var latest = _colorRequests[_colorRequests.Count - 1];
                _requestedHitColor = latest.HitColorValue;
                _requestedHitParamValue = latest.HitParamValue;
            }

            ApplyHitFeedbackState();
        }

        private Color ResolveBaseTintColor(Renderer renderer, bool applyDarkFallback)
        {
            if (applyDarkFallback && _isDark)
                return DarkColor;

            if (_isPoisoned)
                return PoisonBaseColor;

            return _baseColorRecord.TryGetValue(renderer, out var baseColor)
                ? baseColor
                : Color.white;
        }

        private void ApplyToRendererBlocks(Func<MaterialPropertyBlock, Renderer, bool> action)
        {
            if (action == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;

                renderer.GetPropertyBlock(_propertyBlock, 0);
                if (action(_propertyBlock, renderer))
                {
                    renderer.SetPropertyBlock(_propertyBlock, 0);
                }
            }
        }
    }

    public class ActorPresentationComponent : CombatComponentBase
    {
        private const float DefaultBornFadeDuration = 0.35f;
        private const float DefaultDeathFadeDuration = 1f;

        private static readonly string[] DeathStateNames =
        {
            "Death",
            "Dead",
            "Die",
        };

        private CombatShaderController _shaderController;
        private CombatHealthComponent _health;
        private CombatAbilityComponent _ability;
        private CombatMovementComponent _movement;
        private CombatActor _combatActor;
        private IGameplayEffectRuntimeContext _gasRuntimeContext;
        private GameplayTagContainer _ownedTags;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private bool _isBornFadingIn;
        private bool _isDeathPresenting;
        private bool _isFadingOut;
        private bool _isDeathPresentationComplete = true;
        private bool _isDark;
        private bool _isPoisoned;
        private float _bornFadeTimer;
        private float _bornFadeDuration = DefaultBornFadeDuration;
        private float _currentFadeAlpha;
        private float _deathTimer;
        private float _deathAnimationDuration;
        private float _fadeTimer;
        private float _fadeDuration = DefaultDeathFadeDuration;

        public IReadOnlyList<Renderer> Renderers => _renderers;
        public bool IsDeathPresentationComplete => _isDeathPresentationComplete;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _combatActor = owner as CombatActor;
            _health = owner.Get<CombatHealthComponent>();
            _ability = owner.Get<CombatAbilityComponent>();
            _movement = owner.Get<CombatMovementComponent>();
        }

        public override void Initialize()
        {
            base.Initialize();
            UnsubscribePresentationEvents();
            RefreshRendererBindings();
            ResetAllPresentationState();

            if (_health != null)
            {
                _health.OnDeath -= OnHealthDeath;
                _health.OnDeath += OnHealthDeath;
            }

            SubscribeGameplayPresentationEvents();
        }

        public override void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleCommon.PresentationComponent.Update"))
            {
                _shaderController?.TickHitFlash(deltaTime);
                TickBornFadeIn(deltaTime);
                TickDeathPresentation(deltaTime);
            }
        }

        public void RefreshRendererBindings(bool includeInactive = true)
        {
            _renderers = Owner?.GameObject != null
                ? Owner.GameObject.GetComponentsInChildren<Renderer>(includeInactive)
                : Array.Empty<Renderer>();

            RebuildShaderController();
        }

        public void PlayHitFlash()
        {
            _shaderController?.PlayHitFlash();
        }

        public void StopHitFlash()
        {
            _shaderController?.StopHitFlash();
        }

        public void SetDark(bool isDark)
        {
            _isDark = isDark;
            _shaderController?.SetDarkened(isDark);
        }

        public void SetPoisoned(bool isPoisoned)
        {
            _isPoisoned = isPoisoned;
            _shaderController?.SetPoisonTint(isPoisoned);
        }

        public void SetFadeOutAlpha(float alpha)
        {
            _currentFadeAlpha = Mathf.Clamp01(alpha);
            _shaderController?.SetFadeOutAlpha(_currentFadeAlpha);
        }

        public void BeginDeathFadeOut(float duration = DefaultDeathFadeDuration)
        {
            ResetBornFadeInState();
            _movement?.StopMove();
            StopHitFlash();

            _fadeDuration = Mathf.Max(0f, duration);
            _deathAnimationDuration = 0f;
            _deathTimer = 0f;
            _fadeTimer = 0f;
            _isFadingOut = true;
            _isDeathPresenting = true;
            _isDeathPresentationComplete = false;
            SetFadeOutAlpha(_fadeDuration <= 0f ? 1f : 0f);

            if (_fadeDuration <= 0f)
            {
                CompleteDeathPresentation();
            }
        }

        public void AddHitTintRequest(CombatShaderHitModifyRequest request)
        {
            _shaderController?.AddHitTintRequest(request);
        }

        public void RemoveHitTintRequest(int tag)
        {
            _shaderController?.RemoveHitTintRequest(tag);
        }

        public override void DeactivateForPool()
        {
            UnsubscribePresentationEvents();
            _shaderController?.Dispose();
            _shaderController = null;
            _renderers = Array.Empty<Renderer>();
            ResetAllPresentationState();
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            UnsubscribePresentationEvents();
            _shaderController?.Dispose();
            _shaderController = null;
            _renderers = Array.Empty<Renderer>();
            ResetAllPresentationState();
            _health = null;
            _ability = null;
            _movement = null;
            _combatActor = null;
            base.OnDispose();
        }

        private void RebuildShaderController()
        {
            _shaderController?.Dispose();
            _shaderController = new CombatShaderController(_renderers);
            _shaderController.SetDarkened(_isDark);
            _shaderController.SetPoisonTint(_isPoisoned);
            _shaderController.SetFadeOutAlpha(_currentFadeAlpha);
        }

        private void ResetAllPresentationState()
        {
            ResetBornFadeInState();
            ResetDeathPresentationState();
            _isDark = false;
            _isPoisoned = false;
            SetFadeOutAlpha(0f);
        }

        private void SubscribeGameplayPresentationEvents()
        {
            var effects = _ability?.Effects;
            if (effects == null)
                return;

            _gasRuntimeContext = effects.RuntimeContext;
            _gasRuntimeContext?.Subscribe(GameplayEffectEventType.AbilityActivated, OnGameplayAbilityActivated);
            _gasRuntimeContext?.Subscribe(GameplayEffectEventType.AttributeChanged, OnGameplayAttributeChanged);

            _ownedTags = effects.OwnedTags;
            if (_ownedTags != null)
            {
                _ownedTags.RegisterListener(CombatGameplayTags.State_Poisoned, OnPoisonTagChanged);
                SetPoisoned(_ownedTags.HasTag(CombatGameplayTags.State_Poisoned));
            }
        }

        private void OnGameplayAbilityActivated(GameplayEffectEvent gameplayEvent)
        {
            var owner = Owner;
            if (owner == null || gameplayEvent.SourceEntityId != owner.Id)
                return;

            if (gameplayEvent.AbilityId == CombatAbilityIds.Born)
            {
                StartBornFadeIn(ResolveBornFadeInDuration(gameplayEvent.AbilityId));
            }
        }

        private void OnGameplayAttributeChanged(GameplayEffectEvent gameplayEvent)
        {
            var owner = Owner;
            if (owner == null || gameplayEvent.TargetEntityId != owner.Id)
                return;

            if (gameplayEvent.AttributeId == CombatAttributeIds.HP &&
                gameplayEvent.NewValue < gameplayEvent.OldValue &&
                gameplayEvent.NewValue > 0f &&
                !_isDeathPresenting)
            {
                PlayHitFlash();
            }
        }

        private void OnPoisonTagChanged(bool added)
        {
            SetPoisoned(added);
        }

        private void OnHealthDeath(CombatActor killer)
        {
            StartDeathPresentation();
        }

        private void StartDeathPresentation()
        {
            if (_isDeathPresenting)
                return;

            ResetBornFadeInState();
            _movement?.StopMove();
            StopHitFlash();
            SetFadeOutAlpha(0f);

            _deathAnimationDuration = HasActiveDeathAbility() ? 0f : PlayFallbackDeathAnimation();
            _deathTimer = 0f;
            _fadeTimer = 0f;
            _isFadingOut = false;
            _isDeathPresenting = true;
            _isDeathPresentationComplete = false;
        }

        private void StartBornFadeIn(float duration = DefaultBornFadeDuration)
        {
            if (_isDeathPresenting)
                return;

            _bornFadeDuration = Mathf.Max(0f, duration);
            if (_bornFadeDuration <= 0f)
            {
                ResetBornFadeInState();
                SetFadeOutAlpha(0f);
                return;
            }

            _bornFadeTimer = 0f;
            _isBornFadingIn = true;
            SetFadeOutAlpha(1f);
        }

        private void TickBornFadeIn(float deltaTime)
        {
            if (!_isBornFadingIn || _isDeathPresenting)
                return;

            _bornFadeTimer += Mathf.Max(0f, deltaTime);
            float progress = _bornFadeDuration > 0f ? Mathf.Clamp01(_bornFadeTimer / _bornFadeDuration) : 1f;
            SetFadeOutAlpha(1f - progress);

            if (progress >= 1f)
            {
                ResetBornFadeInState();
                SetFadeOutAlpha(0f);
            }
        }

        private void TickDeathPresentation(float deltaTime)
        {
            if (!_isDeathPresenting)
                return;

            deltaTime = Mathf.Max(0f, deltaTime);

            if (!_isFadingOut)
            {
                if (HasActiveDeathAbility())
                    return;

                _deathTimer += deltaTime;
                if (_deathTimer < _deathAnimationDuration)
                    return;

                _isFadingOut = true;
                _fadeTimer = 0f;
            }

            _fadeTimer += deltaTime;
            float alpha = _fadeDuration > 0f ? Mathf.Clamp01(_fadeTimer / _fadeDuration) : 1f;
            SetFadeOutAlpha(alpha);

            if (alpha >= 1f)
            {
                CompleteDeathPresentation();
            }
        }

        private float PlayFallbackDeathAnimation()
        {
            var animator = _combatActor?.Animator;
            if (animator == null || !animator.isActiveAndEnabled)
                return 0.2f;

            for (int i = 0; i < DeathStateNames.Length; i++)
            {
                string stateName = DeathStateNames[i];
                int stateHash = Animator.StringToHash(stateName);
                if (!animator.HasState(0, stateHash))
                    continue;

                animator.CrossFade(stateHash, 0.05f, 0, 0f);
                return Mathf.Max(0.2f, FindAnimationClipLength(animator, stateName));
            }

            return 0.2f;
        }

        private bool HasActiveDeathAbility()
        {
            return _ability?.HasActiveAbility(ability =>
                ability != null &&
                (ability.AbilityId == CombatAbilityIds.Death ||
                 ability.AbilityTag == CombatGameplayTags.Ability_Death)) ?? false;
        }

        private static float FindAnimationClipLength(Animator animator, string stateName)
        {
            var controller = animator.runtimeAnimatorController;
            if (controller?.animationClips == null)
                return 0.2f;

            var clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip != null && string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase))
                {
                    return clip.length;
                }
            }

            return 0.5f;
        }

        private void ResetDeathPresentationState()
        {
            _isDeathPresenting = false;
            _isFadingOut = false;
            _isDeathPresentationComplete = true;
            _deathTimer = 0f;
            _deathAnimationDuration = 0f;
            _fadeTimer = 0f;
            _fadeDuration = DefaultDeathFadeDuration;
        }

        private void ResetBornFadeInState()
        {
            _isBornFadingIn = false;
            _bornFadeTimer = 0f;
            _bornFadeDuration = DefaultBornFadeDuration;
        }

        private void CompleteDeathPresentation()
        {
            _isDeathPresenting = false;
            _isFadingOut = false;
            _isDeathPresentationComplete = true;
            SetFadeOutAlpha(1f);
        }

        private void UnsubscribePresentationEvents()
        {
            if (_health != null)
            {
                _health.OnDeath -= OnHealthDeath;
            }

            _gasRuntimeContext?.Unsubscribe(GameplayEffectEventType.AbilityActivated, OnGameplayAbilityActivated);
            _gasRuntimeContext?.Unsubscribe(GameplayEffectEventType.AttributeChanged, OnGameplayAttributeChanged);
            _gasRuntimeContext = null;

            if (_ownedTags != null)
            {
                _ownedTags.UnregisterListener(OnPoisonTagChanged);
                _ownedTags = null;
            }
        }

        private float ResolveBornFadeInDuration(int abilityId)
        {
            return FindGrantedAbilityDefinition(abilityId) is BornAbilityDefinition bornAbility
                ? bornAbility.FadeInDuration
                : DefaultBornFadeDuration;
        }

        private GameplayAbilityDefinition FindGrantedAbilityDefinition(int abilityId)
        {
            if (abilityId == 0)
                return null;

            return _ability?.FindGrantedAbilityDefinition(abilityId);
        }
    }
}
