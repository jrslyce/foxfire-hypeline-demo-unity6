using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using MuxyGateway;

namespace Foxfire
{
    // Translates Gateway transactions into the same validated, run-scoped dispatcher
    // used by the local simulator. The manager owns authentication and transport.
    public sealed class HypelineGateway : IViewerInteractionGateway
    {
        readonly MuxyGatewayManager manager;
        readonly Func<bool> playable;
        readonly Dictionary<string, GameActionUsed> pending = new Dictionary<string, GameActionUsed>();
        public event Action<ActionRequest> Requested;
        public string Status { get { return Ready ? "HYPELINE CONNECTED" : "HYPELINE DISCONNECTED"; } }
        bool Connected { get { return manager != null && manager.isActiveAndEnabled &&
            manager.TransportState == GatewayTransportState.Connected && manager.IsAuthenticated(); } }
        public bool Ready { get { return Connected && playable(); } }
        public bool IsSimulation { get { return false; } }

        public HypelineGateway(MuxyGatewayManager manager, Func<bool> playable)
        {
            this.manager = manager;
            this.playable = playable;
            manager.OnAnyGameActionUsed.AddListener(OnAction);
        }
        static string Key(string prefix, string value)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var b = new StringBuilder(prefix);
                foreach (byte v in bytes) b.Append(v.ToString("x2"));
                return b.ToString();
            }
        }
        void OnAction(GameActionUsed used)
        {
            if (used == null || string.IsNullOrEmpty(used.TransactionID)) return;
            int index = Array.IndexOf(Actions.Ids, used.ActionID);
            // Other integrations may register their own actions on the same manager.
            if (index < 0) return;
            if (string.IsNullOrEmpty(used.UserID) || currentGame == null || !Ready || Requested == null)
            {
                manager.RefundGameAction(used, "Action unavailable in this game state.");
                return;
            }
            string receipt = Key("hypeline-", used.TransactionID);
            pending[receipt] = used;
            Requested(new ActionRequest { receiptId = receipt, sessionId = Key("viewer-", used.UserID),
                actionId = Actions.Scoped(currentGame.RunId, (ActionKind)index), displayName = used.Username });
        }
        Simulation currentGame;
        public void Tick() { }
        public void Publish(Simulation game, InteractionDispatcher dispatcher)
        {
            currentGame = game;
            if (!Connected) return;
            bool changed = false;
            for (int i = 0; i < Actions.Count; i++)
            {
                GameAction action = manager.FindGameAction(Actions.Ids[i]);
                if (action == null) continue;
                GameActionState state = Ready && dispatcher.Availability((ActionKind)i) == null
                    ? GameActionState.Available : GameActionState.Unavailable;
                if (action.State != state) { action.State = state; changed = true; }
            }
            if (changed) manager.SubmitGameActions();
        }
        public void SendReceipt(Receipt receipt)
        {
            if (receipt == null || receipt.status == "queued") return;
            GameActionUsed used;
            if (!pending.TryGetValue(receipt.id, out used)) return;
            pending.Remove(receipt.id);
            if (receipt.status == "executed") manager.AcceptGameAction(used, "Action applied in Foxfire Arena.");
            else manager.RefundGameAction(used, "Action could not be applied: " + (receipt.reason ?? "unavailable"));
        }
        public void Dispose()
        {
            manager.OnAnyGameActionUsed.RemoveListener(OnAction);
            pending.Clear(); Requested = null;
        }
    }
}
