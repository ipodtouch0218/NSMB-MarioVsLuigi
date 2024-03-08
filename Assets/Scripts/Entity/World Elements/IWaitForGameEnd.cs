using Fusion;
using NSMB.Extensions;

public interface IWaitForGameEnd {

    public virtual FunctionTarget Target => FunctionTarget.All;

    void AttemptExecute(NetworkObject obj) {
        switch (Target) {
        case FunctionTarget.All: {
            Execute();
            break;
        }
        case FunctionTarget.ServerHostOnly: {
            if (obj.Runner.IsServer || obj.Runner.IsSharedModeMasterClient) {
                Execute();
            }

            break;
        }
        case FunctionTarget.ObjectOwnerOnly: {
            if (obj.HasControlAuthority()) {
                Execute();
            }

            break;
        }
        }
    }

    void Execute();

    public enum FunctionTarget {
        All, ServerHostOnly, ObjectOwnerOnly
    }
}
