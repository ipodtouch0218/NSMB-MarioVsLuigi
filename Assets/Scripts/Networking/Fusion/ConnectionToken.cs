using System;
using System.IO;
using System.Text;
using UnityEngine;

using Fusion;
using Newtonsoft.Json;
using NSMB.Utils;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

public struct ConnectionToken : INetworkStruct {

    private static readonly string PublicKey =
@"""
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbVXeDZyyeb+ptn7HEjg0vtIL+jUV
8yOGGaoAPHkDTu3BJVKWSU0O6RdzmsxBujK/5ky6BKN8bUkY4WReEY5oXQ==
-----END PUBLIC KEY-----
""";
    private static ISigner ecdsa;

    [JsonProperty("Nickname"), JsonConverter(typeof(NetworkStringConverter<_32>))]
    public NetworkString<_32> nickname;
    [JsonProperty("SignedData")]
    public SignedResultData signedData;
    [JsonProperty("Signature"), JsonConverter(typeof(NetworkStringConverter<_128>))]
    public NetworkString<_128> signature;

    // SERIALIZATION FORMAT:
    // 0.....19    20...35    36...42   43....................114   115......127
    // nickname    user id     color            signature              unused
    public byte[] Serialize() {

        byte[] buffer = new byte[128];

        byte[] nicknameBytes = Encoding.UTF8.GetBytes(nickname.Value);
        Array.Copy(nicknameBytes, 0, buffer, 0, Mathf.Min(nicknameBytes.Length, 20));

        byte[] userIdBytes = signedData.UserId.ToByteArray();
        Array.Copy(userIdBytes, 0, buffer, 20, 16);

        byte[] nicknameColorBytes = Encoding.UTF8.GetBytes(signedData.NicknameColor.Value);
        Array.Copy(nicknameColorBytes, 0, buffer, 36, Mathf.Min(nicknameColorBytes.Length, 7));

        byte[] signatureBytes = StringUtils.FromBase64(signature.Value);
        Array.Copy(signatureBytes, 0, buffer, 43, Mathf.Min(signatureBytes.Length, 72));

        return buffer;
    }

    public static ConnectionToken Deserialize(byte[] input) {

        int signatureLength = 115 - 43;
        while (input[43 + signatureLength - 1] == 0)
            signatureLength--;

        ConnectionToken newToken = new() {
            nickname = Encoding.UTF8.GetString(input[0..20]).TrimEnd('\0'),
            signedData = new() {
                UserId = new Guid(input[20..36]),
                NicknameColor = Encoding.UTF8.GetString(input[36..43]).TrimEnd('\0'),
            },
            signature = StringUtils.ToBase64(input[43..(43 + signatureLength)]),
        };

        return newToken;
    }

    public bool HasValidSignature() {
        if (ecdsa == null) {

            using StringReader stringReader = new(PublicKey);
            PemReader ecReader = new(stringReader);
            ECPublicKeyParameters ecKeyParameter = (ECPublicKeyParameters) ecReader.ReadObject();

            ecdsa = SignerUtilities.GetSigner("SHA-1withECDSA");
            ecdsa.Init(false, ecKeyParameter);
        }

        string asJson = JsonConvert.SerializeObject(signedData);
        byte[] signedDataBytes = Encoding.UTF8.GetBytes(asJson);

        ecdsa.BlockUpdate(signedDataBytes, 0, signedDataBytes.Length);
        return ecdsa.VerifySignature(StringUtils.FromBase64(signature.Value));
    }

    public struct SignedResultData : INetworkStruct {
        [JsonProperty("UserID")]
        public Guid UserId;
        [JsonProperty("NicknameColor"), JsonConverter(typeof(NetworkStringConverter<_8>))]
        public NetworkString<_8> NicknameColor;
    }

    public class NetworkStringConverter<S> : JsonConverter<NetworkString<S>> where S : unmanaged, IFixedStorage {
        public override NetworkString<S> ReadJson(JsonReader reader, Type objectType, NetworkString<S> existingValue, bool hasExistingValue, JsonSerializer serializer) {
            return (string) reader.Value;
        }

        public override void WriteJson(JsonWriter writer, NetworkString<S> value, JsonSerializer serializer) {
            writer.WriteValue(value.Value);
        }
    }
}

