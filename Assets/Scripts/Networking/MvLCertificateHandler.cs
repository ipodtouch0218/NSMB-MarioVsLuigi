using System;
using System.Text;
using UnityEngine.Networking;

public class MvLCertificateHandler : CertificateHandler {

    protected override bool ValidateCertificate(byte[] certificateData) {
        return true;
        //return base.ValidateCertificate(certificateData);
    }
}