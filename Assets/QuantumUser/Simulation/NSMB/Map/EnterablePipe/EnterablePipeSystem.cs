namespace Quantum {
    public unsafe class EnterablePipeSystem : SystemSignalsOnly {

        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<EnterablePipe, MarioPlayer>(OnPipeMarioInteraction);
        }

        public static void OnPipeMarioInteraction(Frame f, EntityRef pipeEntity, EntityRef marioEntity) {
            var pipe = f.Unsafe.GetPointer<EnterablePipe>(pipeEntity);
            if (!pipe->IsEnterable) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (pipe->IsMiniOnly && mario->CurrentPowerupState != PowerupState.MiniMushroom) {
                return;
            }

            if (mario->IsCrouchedInShell || mario->IsInKnockback || mario->IsStuckInBlock
                || mario->CurrentPowerupState == PowerupState.MegaMushroom || mario->MegaMushroomEndFrames > 0) {
                return;
            }

            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            Input input = default;
            if (mario->PlayerRef.IsValid) {
                input = *f.GetPlayerInput(mario->PlayerRef);
            }

            if (pipe->IsCeilingPipe) {
                if (!marioPhysicsObject->IsTouchingCeiling || !input.Up.IsDown) {
                    return;
                }
            } else {
                if (!marioPhysicsObject->IsTouchingGround || !input.Down.IsDown) {
                    return;
                }
            }

            mario->EnterPipe(f, marioEntity, pipeEntity);
        }
    }
}