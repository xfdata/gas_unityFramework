// public class ActionOpenPopupUI : QueuedActionBase
// {
//     public MediatorBase mediator;
//     public ActionOpenPopupUI(MediatorBase mediator)
//     {
//         IsImmediate = true;
//         ActionType = GameplayActionType.OpenedUI;
//         this.mediator = mediator;
//     }
//
//     public override void Execute()
//     {
//         if (mediator == null || mediator.IsDisposed)
//         {
//             Finish();
//             return;
//         }
//         mediator.OnMediatorDisposeEnd += Finish;
//     }
//
//     protected override void OnDispose()
//     {
//         if(mediator != null)
//             mediator.OnMediatorDisposeEnd -= Finish;
//     }
// }
