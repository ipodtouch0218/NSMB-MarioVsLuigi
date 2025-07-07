#if UNITY_EDITOR || DEVELOPMENT_BUILD
using static Quantum.CommandMvLDebugCmd;

namespace Quantum {

    public unsafe class MvLDebugSystem : SystemMainThread {
        public override void Update(Frame f) {
            for (int i = 0; i < f.PlayerCount; i++) {
                if (f.GetPlayerCommand(i) is CommandMvLDebugCmd cmd) {
                    ExecuteCommand(f, i, cmd);
                }
            }
        }

        private void ExecuteCommand(Frame f, PlayerRef player, CommandMvLDebugCmd cmd) {
            EntityRef marioEntity = EntityRef.None;
            MarioPlayer* mario = null;

            foreach ((var entity, var marioPtr) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (marioPtr->PlayerRef == player) {
                    marioEntity = entity;
                    mario = marioPtr;
                    break;
                }
            }

            if (!f.Exists(marioEntity)) {
                return;
            }

            switch (cmd.CommandId) {
            case DebugCommand.SpawnEntity:
                EntityRef newEntity = f.Create(cmd.SpawnData);
                if (f.Unsafe.TryGetPointer(newEntity, out Transform2D* newEntityTransform)) {
                    newEntityTransform->Position = f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position;
                    newEntityTransform->Position.X += mario->FacingRight ? 1 : -1;
                }
                if (f.Unsafe.TryGetPointer(newEntity, out CoinItem* coinItem)) {
                    coinItem->ParentToPlayer(f, newEntity, marioEntity);
                }
                if (f.Unsafe.TryGetPointer(newEntity, out Enemy* enemy)) {
                    enemy->DisableRespawning = true;
                    enemy->FacingRight = mario->FacingRight;
                    enemy->IsActive = true;
                    enemy->IsDead = false;
                }
                break;
            case DebugCommand.KillSelf:
                mario->Death(f, marioEntity, false, true, EntityRef.None);
                break;
            case DebugCommand.FreezeSelf:
                IceBlockSystem.Freeze(f, marioEntity);
                break;
            }
        }
    }
}
#endif