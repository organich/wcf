﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WcfTestBridgeCommon;

namespace WcfService.CertificateResources
{
    // Resource for generating end-user certificates
    // X509 extensions are added for Subject Alt Name (User Principal Name) support
    // PUT with a comma-separated list of subject names to create a new certificate
    internal class UserCertificateResource : EndCertificateResource
    {
        public UserCertificateResource() : base() { }

        // Requests a certificate to be generated by the Bridge based on a user name and not machine name
        public override ResourceResponse Put(ResourceRequestContext context)
        {
            X509Certificate2 certificate;

            string subject; 
            if (!context.Properties.TryGetValue(subjectKeyName, out subject) || string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentException("When PUTting to this resource, specify an non-empty 'subject'", "context.Properties");
            }

            // There can be multiple subjects, separated by ,
            string[] subjects = subject.Split(',');

            lock (s_certificateResourceLock)
            {
                if (!s_createdCertsBySubject.TryGetValue(subjects[0], out certificate))
                {
                    CertificateGenerator generator = CertificateResourceHelpers.GetCertificateGeneratorInstance(context.BridgeConfiguration);

                    CertificateCreationSettings certificateCreationSettings = new CertificateCreationSettings() { Subjects = subjects };
                    certificate = generator.CreateUserCertificate(certificateCreationSettings).Certificate;
                    
                    // Cache the certificates
                    s_createdCertsBySubject.Add(subjects[0], certificate);
                    s_createdCertsByThumbprint.Add(certificate.Thumbprint, certificate);

                    // Created certs get put onto the local machine
                    // We ideally don't want this to happen, but until we find a way to have BridgeClient not need elevation for cert installs
                    // we need this to happen so that running locally doesn't require elevation as it messes up our CI and developer builds
                    CertificateManager.InstallCertificateToMyStore(certificate);
                }
            }

            ResourceResponse response = new ResourceResponse();
            response.Properties.Add(thumbprintKeyName, certificate.Thumbprint);

            return response;
        }
    }
}
