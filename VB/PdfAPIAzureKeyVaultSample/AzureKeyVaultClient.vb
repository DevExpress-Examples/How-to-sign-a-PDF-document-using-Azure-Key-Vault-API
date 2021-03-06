﻿Imports Azure.Core
Imports Azure.Identity
Imports Azure.Security.KeyVault.Certificates
Imports Azure.Security.KeyVault.Keys
Imports Azure.Security.KeyVault.Keys.Cryptography
Imports DevExpress.Office.DigitalSignatures
Imports DevExpress.Office.Tsp
Imports DevExpress.Pdf
Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Runtime.Serialization
Imports System.Text



Namespace PdfAPIAzureKeyVaultSample
	Public Class AzureKeyVaultSigner
		Inherits Pkcs7SignerBase

		'OID for RSA signing algorithm:
		Private Const PKCS1RsaEncryption As String = "1.2.840.113549.1.1.1"

		Private ReadOnly keyVaultClient As AzureKeyVaultClient
		Private ReadOnly keyId As String
		Private ReadOnly certificateChain()() As Byte


		'Must match with key algorithm (RSA or ECDSA)
		'For RSA PKCS1RsaEncryption(1.2.840.113549.1.1.1) OID can be used with any digest algorithm
		'For ECDSA use OIDs from this family http://oid-info.com/get/1.2.840.10045.4.3 
		'Specified digest algorithm must be same with DigestCalculator algorithm.
		Protected Overrides ReadOnly Property DigestCalculator() As IDigestCalculator
			Get
				Return New DevExpress.Office.DigitalSignatures.DigestCalculator(HashAlgorithmType.SHA256) 'Digest algorithm
			End Get
		End Property
		Protected Overrides ReadOnly Property SigningAlgorithmOID() As String
			Get
				Return PKCS1RsaEncryption
			End Get
		End Property
		Protected Overrides Function GetCertificates() As IEnumerable(Of Byte())
			Return certificateChain
		End Function

		Public Sub New(ByVal keyVaultClient As AzureKeyVaultClient, ByVal certificateIdentifier As String, ByVal keyId As String, Optional ByVal tsaClient As ITsaClient = Nothing, Optional ByVal ocspClient As IOcspClient = Nothing, Optional ByVal crlClient As ICrlClient = Nothing, Optional ByVal profile As PdfSignatureProfile = PdfSignatureProfile.Pdf)
			MyBase.New(tsaClient, ocspClient, crlClient, profile)
			Me.keyVaultClient = keyVaultClient
			Me.keyId = keyId
			'Get certificate (without piblic key) from Azure Key Vault storage via CertificateClient API or create a new one at runtime
			'You can get the whole certificate chain here
			certificateChain = New Byte()() { keyVaultClient.GetCertificateData(keyId, certificateIdentifier) }
		End Sub
		Protected Overrides Function SignDigest(ByVal digest() As Byte) As Byte()
			Return keyVaultClient.Sign(SignatureAlgorithm.RS256, digest)
		End Function
	End Class

	Public Class AzureKeyVaultClient
		Private Const rsaKeyId As String = "" 'specify name of Key Vault's RSA key here
'        
'         * Alternatively, you can create a temporary RSA key:
'         * rsaKeyId = $"CloudRsaKey-{Guid.NewGuid()}";
'            var rsaKeyOptions = new CreateRsaKeyOptions(rsaKeyName, hardwareProtected: false)
'            {
'                KeySize = 2048,
'            };
'         
		Public Shared Function CreateClient(ByVal keyVaultUrl As String) As AzureKeyVaultClient
			Return New AzureKeyVaultClient(New KeyClient(New Uri(keyVaultUrl), New DefaultAzureCredential()))
		End Function

		Private ReadOnly client As KeyClient

		Private defaultAzureCredential As DefaultAzureCredential
		Private Sub New(ByVal client As KeyClient)
			Me.client = client

			Dim credentialOptions = New DefaultAzureCredentialOptions With {
				.ExcludeInteractiveBrowserCredential = False,
				.ExcludeVisualStudioCodeCredential = True
			}
			defaultAzureCredential = New DefaultAzureCredential(credentialOptions)
		End Sub
	   Public Function Sign(ByVal algorithm As Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm, ByVal digest() As Byte) As Byte()
			Dim cloudRsaKey As KeyVaultKey = client.GetKey(rsaKeyId)
			Dim rsaCryptoClient = New CryptographyClient(cloudRsaKey.Id, defaultAzureCredential)

			Dim rsaSignResult As SignResult = rsaCryptoClient.Sign(algorithm, digest)
			Debug.WriteLine($"Signed digest using the algorithm {rsaSignResult.Algorithm}, with key {rsaSignResult.KeyId}. " & $"The resulting signature is {Convert.ToBase64String(rsaSignResult.Signature)}")

			Return rsaSignResult.Signature
	   End Function

		Public Function GetCertificateData(ByVal keyVaultUrl As String, ByVal certificateIdentifier As String) As Byte()
			Dim certificateClient As New CertificateClient(New Uri(keyVaultUrl), defaultAzureCredential)
			Dim cert As KeyVaultCertificateWithPolicy = certificateClient.GetCertificate(certificateIdentifier)

			Return cert.Cer
		End Function


	End Class
End Namespace
