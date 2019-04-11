﻿using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace Kamus.KeyManagement
{
    public class EnvelopeEncryptionDecorator : IKeyManagement
    {
        private readonly IKeyManagement mMasterKeyManagement;
        private readonly int mMaximumDataLength;
        private readonly ILogger mLogger = Log.ForContext<EnvelopeEncryptionDecorator>();

        public EnvelopeEncryptionDecorator(IKeyManagement masterKeyManagement, int maximumDataLength)
        {
            mMasterKeyManagement = masterKeyManagement;
            mMaximumDataLength = maximumDataLength;
        }

        public async Task<string> Encrypt(string data, string serviceAccountId, bool createKeyIfMissing = true)
        {
            if (data.Length <= mMaximumDataLength)
            {
                return await mMasterKeyManagement.Encrypt(data, serviceAccountId, createKeyIfMissing);
            }

            mLogger.Information("Encryption data too length, using envelope encryption");

            var dataKey = RijndaelUtils.GenerateKey(256);
            var (encryptedData, iv) = RijndaelUtils.Encrypt(dataKey, Encoding.UTF8.GetBytes(data));
            var encryptedDataKey = await mMasterKeyManagement.Encrypt(Convert.ToBase64String(dataKey), serviceAccountId, createKeyIfMissing);
            return $"env${encryptedDataKey}${Convert.ToBase64String(iv)}:{Convert.ToBase64String(encryptedData)}";

        }

        public async Task<string> Decrypt(string encryptedData, string serviceAccountId)
        {
            var splitted = encryptedData.Split('$');
            var regex = new Regex("env\\$(?<encryptedDataKey>.*)\\$(?<iv>.*):(?<encryptedData>.*)");
            var match = regex.Match(encryptedData);

            if (!match.Success)
            {
                throw new InvalidOperationException("Invalid encrypted data format");
            }

            var encryptedDataKey = match.Groups["encryptedDataKey"].Value;
            var actualEncryptedData = Convert.FromBase64String(match.Groups["encryptedData"].Value);
            var iv = Convert.FromBase64String(match.Groups["iv"].Value);

            mLogger.Information("Encrypted data mactched envelope encryption pattern");

            var key = await mMasterKeyManagement.Decrypt(encryptedDataKey, serviceAccountId);

            var decrypted = RijndaelUtils.Decrypt(Convert.FromBase64String(key), iv, actualEncryptedData);
            return Encoding.UTF8.GetString(decrypted);

        }
    }
}
