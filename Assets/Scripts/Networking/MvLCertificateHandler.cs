using UnityEngine.Networking;

public class MvLCertificateHandler : CertificateHandler {

    protected override bool ValidateCertificate(byte[] certificateData) => true;

}