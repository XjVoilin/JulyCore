using JulyCore.Core;
using JulyCore.Provider.Base;

namespace JulyCore.Provider.Encryption
{
    internal class NoEncryptionProvider : ProviderBase, IEncryptionProvider
    {
        public override int Priority => Frameworkconst.PriorityEncryptionProvider;
        protected override LogChannel LogChannel => LogChannel.Encryption;

        public byte[] Encrypt(byte[] data) => data;

        public byte[] Decrypt(byte[] encryptedData) => encryptedData;
    }
}
