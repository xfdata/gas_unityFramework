using UnityEngine;

namespace BattleSkillSimulation
{
    /// <summary>
    /// Scene entry for the Battle + GAS skill simulation.
    /// Attach it to an empty GameObject and press Play.
    /// </summary>
    public class BattleSkillSimulationController : MonoBehaviour
    {
        [Header("Scene Actors")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform npcRoot;
        [SerializeField] private bool createPrimitiveActors = true;

        [Header("Player Input")]
        [SerializeField] private KeyCode castSkillKey = KeyCode.J;
        [SerializeField] private KeyCode resetKey = KeyCode.R;

        [Header("Skill")]
        [SerializeField] private float skillRange = 2.2f;
        [SerializeField] private float skillRadius = 0.75f;
        [SerializeField] private float skillAttackFactor = 1f;
        [SerializeField] private float skillCooldown = 0.35f;

        [Header("Stats")]
        [SerializeField] private float playerMaxHP = 100f;
        [SerializeField] private float playerAttack = 25f;
        [SerializeField] private float playerMoveSpeed = 4f;
        [SerializeField] private float npcMaxHP = 150f;
        [SerializeField] private float npcDefense = 2f;

        private BattleSkillSimulationWorld _world;

        private void Start()
        {
            BuildWorld();
        }

        private void Update()
        {
            if (_world == null)
                return;

            _world.MovePlayer(ReadMoveInput(), Time.deltaTime);

            if (Input.GetKeyDown(castSkillKey))
                _world.CastSkill(Time.time);

            if (Input.GetKeyDown(resetKey))
                BuildWorld();

            _world.Update(Time.deltaTime);
        }

        private void OnDestroy()
        {
            DisposeWorld();
        }

        private void OnGUI()
        {
            if (_world == null)
                return;

            GUILayout.BeginArea(new Rect(16f, 16f, 360f, 160f), GUI.skin.box);
            GUILayout.Label("Battle Skill Simulation");
            GUILayout.Label($"Player HP: {_world.PlayerHP:0}/{_world.PlayerMaxHP:0}");
            GUILayout.Label($"NPC HP: {_world.NpcHP:0}/{_world.NpcMaxHP:0}");
            GUILayout.Label(_world.LastMessage);
            GUILayout.EndArea();
        }

        private void BuildWorld()
        {
            DisposeWorld();

            var request = new BattleSkillSimulationWorldBuilder.BuildRequest
            {
                PlayerRoot = playerRoot,
                NpcRoot = npcRoot,
                CreatePrimitiveActors = createPrimitiveActors,
                PlayerMaxHP = playerMaxHP,
                PlayerAttack = playerAttack,
                PlayerMoveSpeed = playerMoveSpeed,
                NpcMaxHP = npcMaxHP,
                NpcDefense = npcDefense,
                SkillRange = skillRange,
                SkillRadius = skillRadius,
                SkillAttackFactor = skillAttackFactor,
                SkillCooldown = skillCooldown,
            };

            _world = new BattleSkillSimulationWorldBuilder().Build(request);
        }

        private void DisposeWorld()
        {
            _world?.Dispose();
            _world = null;
        }

        private static Vector3 ReadMoveInput()
        {
            return new Vector3(
                Input.GetAxisRaw("Horizontal"),
                0f,
                Input.GetAxisRaw("Vertical"));
        }
    }
}
