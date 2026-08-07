using System;
using System.Collections.Generic;

namespace BattleFoundation
{
    public abstract class BattleCommand
    {
        public int SourceEntityId { get; protected set; }
        public int TargetEntityId { get; protected set; }
        public int CommandFrame { get; set; }
        public byte CommandType { get; protected set; }
        public bool IsConsumed { get; private set; }
        public long CommandSequence { get; private set; }

        public BattleCommand()
        {
            CommandType = GetCommandTypeId();
        }

        protected abstract byte GetCommandTypeId();

        public void Execute(BattleEngine engine)
        {
            if (IsConsumed) return;
            IsConsumed = true;
            OnExecute(engine);
        }

        protected abstract void OnExecute(BattleEngine engine);

        /// <summary>
        /// Returns the deterministic payload needed to recreate this command in a replay.
        /// Derived commands should override this with a stable, versioned representation.
        /// </summary>
        public virtual string SerializePayload() => null;

        /// <summary>
        /// Restores the payload produced by <see cref="SerializePayload"/>.
        /// Stateless commands can keep the default null-or-empty implementation.
        /// </summary>
        public virtual bool DeserializePayload(string payload) => string.IsNullOrEmpty(payload);

        internal void Schedule(int commandFrame, long sequence)
        {
            CommandFrame = commandFrame;
            CommandSequence = sequence;
        }

        internal bool RestoreFromRecord(BattleCommandRecord record)
        {
            if (record == null || record.CommandType != CommandType)
                return false;

            SourceEntityId = record.SourceEntityId;
            TargetEntityId = record.TargetEntityId;
            IsConsumed = false;
            Schedule(record.CommandFrame, record.CommandSequence);
            return DeserializePayload(record.Payload);
        }

        public BattleCommandRecord ToRecord()
        {
            return new BattleCommandRecord
            {
                CommandFrame = CommandFrame,
                CommandSequence = CommandSequence,
                CommandType = CommandType,
                SourceEntityId = SourceEntityId,
                TargetEntityId = TargetEntityId,
                Payload = SerializePayload(),
            };
        }

        public virtual void Reset()
        {
            SourceEntityId = 0;
            TargetEntityId = 0;
            CommandFrame = 0;
            CommandSequence = 0;
            IsConsumed = false;
        }
    }

    [Serializable]
    public class BattleCommandRecord
    {
        public int CommandFrame;
        public long CommandSequence;
        public byte CommandType;
        public int SourceEntityId;
        public int TargetEntityId;
        public string Payload;
    }

    /// <summary>
    /// Battle-mode supplied factory for replaying concrete command types without
    /// adding game-specific command dependencies to BattleFoundation.
    /// </summary>
    public interface IBattleCommandFactory
    {
        BattleCommand CreateCommand(byte commandType);
    }

    public class CommandQueue
    {
        private readonly List<BattleCommand> _commands = new List<BattleCommand>();
        private long _nextSequence;

        public int Count => _commands.Count;

        public void Enqueue(BattleCommand command)
        {
            if (command == null)
                return;

            command.Schedule(command.CommandFrame, _nextSequence++);
            int index = _commands.Count;
            while (index > 0 && Compare(command, _commands[index - 1]) < 0)
                index--;
            _commands.Insert(index, command);
        }

        public bool TryDequeueDue(int frameIndex, out BattleCommand command)
        {
            if (_commands.Count > 0 && _commands[0].CommandFrame <= frameIndex)
            {
                command = _commands[0];
                _commands.RemoveAt(0);
                return true;
            }

            command = null;
            return false;
        }

        private static int Compare(BattleCommand left, BattleCommand right)
        {
            int frameComparison = left.CommandFrame.CompareTo(right.CommandFrame);
            return frameComparison != 0
                ? frameComparison
                : left.CommandSequence.CompareTo(right.CommandSequence);
        }

        public void Clear()
        {
            _commands.Clear();
        }
    }
}
