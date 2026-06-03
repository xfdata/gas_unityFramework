using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class ViewBase : UIModuleBase
    {
        public GameObject GameObject { get; private set; }
        public Transform Transform { get; private set; }
        public RectTransform RectTransform { get; private set; }
        public UIWindow Window { get; private set; }
        public UIViewConfig Config => Window.Config;

        private Vector3 _rootOffset;
        private Vector3 _originSize;
        private Transform _blurTransform;

        internal void BindView(UIWindow window, GameObject go, UIViewBinder binder)
        {
            Window = window;
            GameObject = go;
            Transform = go.transform;
            RectTransform = Transform as RectTransform;

            if (binder != null)
                B = binder;
            else
                UIViewAutoBind.Bind(this, Transform);

            OnBind();
        }

        protected virtual void OnBind()
        {
        }

        internal UniTask OpenInternal(object param)
        {
            return OnOpen(param);
        }

        internal UniTask RefreshInternal(object param)
        {
            return OnRefresh(param);
        }

        internal UniTask CloseInternal(object result)
        {
            return OnClose(result);
        }

        internal UniTask PlayOpenAnimationInternal()
        {
            return PlayOpenAnimation();
        }

        internal UniTask PlayCloseAnimationInternal()
        {
            return PlayCloseAnimation();
        }

        internal void ShownInternal()
        {
            OnShown();
        }

        internal bool EscInternal()
        {
            return OnEsc();
        }

        protected virtual UniTask OnOpen(object param)
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask OnRefresh(object param)
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask OnClose(object result)
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask PlayOpenAnimation()
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask PlayCloseAnimation()
        {
            return UniTask.CompletedTask;
        }

        protected virtual void OnShown()
        {
        }

        protected virtual bool OnEsc()
        {
            return false;
        }

        protected void Close(object result = null)
        {
            Window.CloseAsync(result).Forget();
        }

        protected UniTask<TView> Open<TView>(object param = null) where TView : ViewBase
        {
            return Context.Runtime.Open<TView>(param);
        }

        internal void AttachBlurTransform(Transform blurTransform)
        {
            if (blurTransform == null)
                return;

            _blurTransform = blurTransform;
            _blurTransform.SetParent(Transform, false);
            _blurTransform.SetAsFirstSibling();
            _blurTransform.localPosition = Vector3.zero;
            _blurTransform.localScale = Vector3.one;
            AdaptBlurTransform();
        }

        public void AdaptRootTransform()
        {
            if (RectTransform == null || RectTransform.parent == null)
                return;

            if (!(RectTransform.parent is RectTransform parentRect))
            {
                Debug.LogWarning($"[ViewBase] Cannot adapt {GameObject.name}; its parent is not a RectTransform.");
                return;
            }

            var sizeDelta = RectTransform.sizeDelta;
            var anchorMax = RectTransform.anchorMax;
            var anchorMin = RectTransform.anchorMin;
            var parentSize = parentRect.rect.size;

            _originSize = sizeDelta + parentSize * (anchorMax - anchorMin);

            parentSize = new Vector2(parentSize.x - Context.Root.SideOffset * 2f, parentSize.y);
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentSize.x);

            OnAdaptRoot();
            AdaptBlurTransform();
        }

        protected virtual void OnAdaptRoot()
        {
            if (Config.IgnoreSafeArea)
            {
                RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _originSize.y);
                RectTransform.localPosition = _rootOffset;
                return;
            }

            if (Screen.height <= 0)
                return;

            float top = (Screen.height - Screen.safeArea.yMax) * _originSize.y / Screen.height;
            float bottom = Screen.safeArea.yMin * _originSize.y / Screen.height;

            _rootOffset = new Vector3(0f, (bottom - top) * 0.5f, 0f);
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _originSize.y - top - bottom);
            RectTransform.localPosition = _rootOffset;
        }

        private void AdaptBlurTransform()
        {
            if (_blurTransform == null)
                return;

            if (_blurTransform is RectTransform rect)
            {
                rect.sizeDelta = new Vector2(_originSize.x, _originSize.y);
                rect.localPosition = -_rootOffset;
            }
        }
    }

    public abstract class ViewBase<TParam> : ViewBase
    {
        protected sealed override UniTask OnOpen(object param)
        {
            return OnOpen(param is TParam p ? p : default);
        }

        protected virtual UniTask OnOpen(TParam param)
        {
            return UniTask.CompletedTask;
        }

        protected sealed override UniTask OnRefresh(object param)
        {
            return OnRefresh(param is TParam p ? p : default);
        }

        protected virtual UniTask OnRefresh(TParam param)
        {
            return UniTask.CompletedTask;
        }
    }

    public abstract class ViewBase<TParam, TBinder> : ViewBase<TParam>
        where TBinder : UIViewBinder
    {
        protected new TBinder B => (TBinder)base.B;
    }
