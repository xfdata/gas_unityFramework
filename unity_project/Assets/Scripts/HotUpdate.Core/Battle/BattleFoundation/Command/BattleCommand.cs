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

        public virtual void Reset()
        {
            SourceEntityId = 0;
            TargetEntityId = 0;
            CommandFrame = 0;
            IsConsumed = false;
        }
    }

    public class CommandQueue
    {
        private Queue<BattleCommand> _queue = new Queue<BattleCommand>();

        public int Count => _queue.Count;

        public void Enqueue(BattleCommand command)
        {
            _queue.Enqueue(command);
        }

        public bool TryDequeue(out BattleCommand command)
        {
            if (_queue.Count > 0)
            {
                command = _queue.Dequeue();
                return true;
            }
            command = null;
            return false;
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}