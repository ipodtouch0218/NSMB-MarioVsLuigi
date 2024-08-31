namespace Quantum {
    public unsafe class EnterablePipeSystem : SystemSignalsOnly {

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<EnterablePipe, MarioPlayer>(OnPipeMarioInteraction);
        }

        public static void OnPipeMarioInteraction(Frame f, EntityRef pipeEntity, EntityRef marioEntity) {
            var pipe = f.Get<EnterablePipe>(pipeEntity);
            if (!pipe.IsEnterable) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (mario->IsCrouchedInShell) {
                return;
            }

            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            Input input = default;
            if (mario->PlayerRef.IsValid) {
                input = *f.GetPlayerInput(mario->PlayerRef);
            }

            if (pipe.IsCeilingPipe) {
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