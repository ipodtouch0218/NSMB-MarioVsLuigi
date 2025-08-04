using Miniscript;
using Photon.Deterministic;
using Quantum;
using System;

public unsafe class ValFrame : ValMap {

    private static Intrinsic Frame_GetStageTile, Frame_GetPlayerData, Frame_GetPlayerInput, Frame_Create, Frame_Exists, Frame_Destroy, Frame_FindAsset, Frame_Add, Frame_Get, Frame_Has, Frame_Remove;

    static ValFrame() {
        Frame_GetStageTile = Intrinsic.Create("");
        Frame_GetStageTile.AddParam("x");
        Frame_GetStageTile.AddParam("y");
        Frame_GetStageTile.code = (context, partial) => {
            int x = context.GetLocalInt("x");
            int y = context.GetLocalInt("y");
            Frame f = context.interpreter.hostData as Frame;
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (x < 0 || x >= stage.TileDimensions.X || y < 0 || y >= stage.TileDimensions.Y) {
                return Intrinsic.Result.Null;
            }
            int index = x + y * stage.TileDimensions.X;

            return new Intrinsic.Result(new ValStructPtr(typeof(StageTileInstance), &f.StageTiles[index]));
        };

        Frame_GetPlayerData = Intrinsic.Create("");
        Frame_GetPlayerData.AddParam("player");
        Frame_GetPlayerData.code = (context, partial) => {
            int playerRef = context.GetLocalInt("player");
            Frame f = context.interpreter.hostData as Frame;
            var dict = f.ResolveDictionary(f.Global->PlayerDatas);

            if (!dict.ContainsKey(playerRef)) {
                return Intrinsic.Result.Null;
            }

            /*
            ValMap result = PlayerData.ShallowCopy();
            result.userData = dict[playerRef];
            return new Intrinsic.Result(result);
            */
            return null;
        };

        Frame_GetPlayerInput = Intrinsic.Create("");
        Frame_GetPlayerInput.AddParam("player");
        Frame_GetPlayerInput.code = (context, partial) => {
            int playerRef = context.GetLocalInt("player");
            Frame f = context.interpreter.hostData as Frame;

            Input* input = f.GetPlayerInput(playerRef);
            if (input == null) {
                return Intrinsic.Result.Null;
            }
            
            return new Intrinsic.Result(new ValStructPtr(typeof(Input), input));
        };

            Frame_Create = Intrinsic.Create("");
        Frame_Create.AddParam("assetRef");
        Frame_Create.code = (context, partial) => {
            Value param = context.GetLocal("assetRef", null);
            Frame f = context.interpreter.hostData as Frame;
            EntityRef newEntity;

            if (param is ValAssetRef valAssetObject) {
                if (f.Context.ResourceManager.TryGetAsset(valAssetObject.Asset, out EntityPrototype ep)) {
                    // Create from prototype
                    newEntity = f.Create(ep);
                } else {
                    // Failure, do nothing
                    return Intrinsic.Result.Null;
                }
            } else if (param == null) {
                // Create empty
                newEntity = f.Create();
            } else {
                // Do nothing- invalid param
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(new ValEntityRef { EntityRef = newEntity });
        };

        Frame_Exists = Intrinsic.Create("");
        Frame_Exists.AddParam("entityRef");
        Frame_Exists.code = (context, partial) => {
            EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
            Frame f = context.interpreter.hostData as Frame;
            return f.Exists(entity) ? Intrinsic.Result.True : Intrinsic.Result.False;
        };

        Frame_Destroy = Intrinsic.Create("");
        Frame_Destroy.AddParam("entityRef");
        Frame_Destroy.code = (context, partial) => {
            EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
            Frame f = context.interpreter.hostData as Frame;
            return f.Destroy(entity) ? Intrinsic.Result.True : Intrinsic.Result.False;
        };

        Frame_FindAsset = Intrinsic.Create("");
        Frame_FindAsset.AddParam("path");
        Frame_FindAsset.code = (context, partial) => {
            string path = context.GetLocalString("path");
            Frame f = context.interpreter.hostData as Frame;
            var asset = f.Context.ResourceManager.GetAsset(path);
            return new Intrinsic.Result(new ValAssetRef { Asset = asset });
        };

        Frame_Add = Intrinsic.Create("");
        Frame_Add.AddParam("entityRef");
        Frame_Add.AddParam("type");
        Frame_Add.code = (context, partial) => {
            EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
            int id = ComponentTypeId.GetComponentIndex(context.GetLocalString("componentTypeName"));
            Frame f = context.interpreter.hostData as Frame;
            try {
                int index = ComponentTypeId.GetComponentIndex(context.GetLocalString("componentTypeName"));
                var result = f.Add(entity, index, out _);
                return (result is AddResult.ComponentAdded or AddResult.ComponentAlreadyExists) ? Intrinsic.Result.True : Intrinsic.Result.False;
            } catch {
                return Intrinsic.Result.False;
            }
        };

        Frame_Get = Intrinsic.Create("");
        Frame_Get.AddParam("entityRef");
        Frame_Get.AddParam("type");
        Frame_Get.code = (context, partial) => {
            EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
            Frame f = context.interpreter.hostData as Frame;
            try {
                string typeStr = context.GetLocalString("type");
                int index = ComponentTypeId.GetComponentIndex(typeStr);
                Type type = ComponentTypeId.GetComponentType(index);

                if (!f.Unsafe.TryGetPointer(entity, index, out void* ptr)) {
                    return Intrinsic.Result.Null;
                }
                return new Intrinsic.Result(new ValStructPtr(type, ptr));
            } catch {
                return Intrinsic.Result.Null;
            }
        };

        Frame_Has = Intrinsic.Create("");
        Frame_Has.AddParam("entityRef");
        Frame_Has.AddParam("type");
        Frame_Has.code = (context, partial) => {
            EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
            Frame f = context.interpreter.hostData as Frame;
            try {
                int index = ComponentTypeId.GetComponentIndex(context.GetLocalString("type"));
                return f.Has(entity, index) ? Intrinsic.Result.True : Intrinsic.Result.False;
            } catch {
                return Intrinsic.Result.False;
            }
        };

        Frame_Remove = Intrinsic.Create("");
        Frame_Remove.AddParam("entityRef");
        Frame_Remove.AddParam("type");
        Frame_Remove.code = (context, partial) => {
            EntityRef entity = (context.GetLocal("entityRef") as ValEntityRef)?.EntityRef ?? EntityRef.None;
            Frame f = context.interpreter.hostData as Frame;
            try {
                int index = ComponentTypeId.GetComponentIndex(context.GetLocalString("type"));
                return f.Remove(entity, index) ? Intrinsic.Result.True : Intrinsic.Result.False;
            } catch {
                return Intrinsic.Result.False;
            }
        };
    }

    public Frame Frame;

	public ValFrame() {
        evalOverride = (ValMap self, Value key, out Value outValue) => {
            outValue = null;
            Frame f = self.userData as Frame;
            switch (key.ToString()) {
            case "number": outValue = new ValNumber(f.Number); break;
            case "isVerified": outValue = f.IsVerified ? ValNumber.one : ValNumber.zero; break;
            case "isPredicted": outValue = f.IsPredicted ? ValNumber.one : ValNumber.zero; break;
            case "deltaTime": outValue = new ValNumber(f.DeltaTime); break;
            case "playerCount": outValue = new ValNumber(f.ComponentCount<PlayerData>()); break;
            case "rng": outValue = new ValNumber(f.RNG->Next()); break;
            case "getStageTile": outValue = Frame_GetStageTile.GetFunc(); break;
            case "getPlayerData": outValue = Frame_GetPlayerData.GetFunc(); break;
            case "getPlayerInput": outValue = Frame_GetPlayerInput.GetFunc(); break;
            case "create": outValue = Frame_Create.GetFunc(); break;
            case "exists": outValue = Frame_Exists.GetFunc(); break;
            case "destroy": outValue = Frame_Destroy.GetFunc(); break;
            case "findAsset": outValue = Frame_FindAsset.GetFunc(); break;
            case "add": outValue = Frame_Add.GetFunc(); break;
            case "has": outValue = Frame_Has.GetFunc(); break;
            case "get": outValue = Frame_Get.GetFunc(); break;
            case "remove": outValue = Frame_Remove.GetFunc(); break;
            }
            return true;
        };
    }

    public override void Serialize(FrameSerializer serializer) {
        // Nothing.
    }

    public override FP Equality(Value rhs) {
        // All frame instances are the same.
        return this.GetType() == rhs.GetType() ? 1 : 0; 
    }
}