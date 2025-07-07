namespace Quantum {
    public interface ILobbyCommand {
        unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData);
    }
}