using UnityEngine.Networking;

namespace NSMB.Networking {
    public class MvLCertificateHandler : CertificateHandler {

        // Bodge to fix certificate issues around Windows 7
        // Potentially insecure, I don't care!
        protected override bool ValidateCertificate(byte[] certificateData) => true;

    }
}