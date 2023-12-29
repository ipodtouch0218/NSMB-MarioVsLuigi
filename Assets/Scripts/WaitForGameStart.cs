using Fusion;

public abstract class WaitForGameStart : NetworkBehaviour {

    public FunctionTarget target = FunctionTarget.All;
    public void AttemptExecute() {

        switch (target) {
        case FunctionTarget.All: {
            Execute();
            break;
        }
        case FunctionTarget.ServerHostOnly: {
            if (Runner.IsServer)
                Execute();
            break;
        }
        case FunctionTarget.ObjectOwnerOnly: {
            if (Object.HasInputAuthority)
                Execute();
            break;
        }
        }
    }
    public abstract void Execute();
    public enum FunctionTarget {
        All, ServerHostOnly, ObjectOwnerOnly
    }
}
