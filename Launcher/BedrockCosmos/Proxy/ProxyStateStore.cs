using Newtonsoft.Json;
using System.IO;

namespace BedrockCosmos.Proxy
{
    public sealed class ProxyStateStore : IProxyStateStore
    {
        private readonly string stateFilePath;

        public ProxyStateStore(string stateFilePath)
        {
            this.stateFilePath = stateFilePath;
        }

        public ProxyOwnershipState Load()
        {
            if (!File.Exists(stateFilePath))
                return null;

            string json = File.ReadAllText(stateFilePath);
            return JsonConvert.DeserializeObject<ProxyOwnershipState>(json);
        }

        public void Save(ProxyOwnershipState state)
        {
            string directory = Path.GetDirectoryName(stateFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(stateFilePath, json);
        }

        public void Delete()
        {
            if (File.Exists(stateFilePath))
                File.Delete(stateFilePath);
        }
    }
}
