using Fusion;

public interface IWaitForGameStart {

    public virtual FunctionTarget Target => FunctionTarget.All;

    void AttemptExecute(NetworkObject obj) {
        switch (Target) {
        case FunctionTarget.All: {
            Execute();
            break;
        }
        case FunctionTarget.ServerHostOnly: {
            if (obj.Runner.IsServer) {
                Execute();
            }

            break;
        }
        case FunctionTarget.ObjectOwnerOnly: {
            if (obj.HasInputAuthority) {
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
