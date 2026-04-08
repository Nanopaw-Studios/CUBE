using System;
using System.IO.Pipes;
using System.Text;
using UnityEngine;

public class LaymsDiscordRPC : MonoBehaviour
{
    public string applicationId = "YOUR_APP_ID_HERE";

    private NamedPipeClientStream pipe;
    public bool Connected { get; private set; }

    public static LaymsDiscordRPC current { get; private set; }

    private void Awake()
    {
        if (current != null && current != this) { Destroy(gameObject); return; }
        current = this;
        DontDestroyOnLoad(gameObject);
        Connect();
    }

    private void Connect()
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.None);
                pipe.Connect(1000);
                Handshake();
                Connected = true;
                Debug.Log($"[RPC] Connected on discord-ipc-{i}");
                return;
            }
            catch
            {
                pipe?.Dispose();
                pipe = null;
            }
        }
        Debug.LogWarning("[RPC] Could not connect to Discord. Is Discord running?");
    }

    private void Handshake()
    {
        Write(0, $"{{\"v\":1,\"client_id\":\"{applicationId}\"}}");
        Read(); // consume Discord's READY response
    }

    public void SetPresence(string state, string details,
                            string largeImageKey = "", string largeImageText = "",
                            string smallImageKey = "", string smallImageText = "",
                            int partySize = 0, int partyMax = 0, string partyId = "",
                            string joinSecret = "")
    {
        if (!Connected) return;

        // Build assets block
        string assets = "";
        if (!string.IsNullOrEmpty(largeImageKey))
        {
            string smallFields = "";
            if (!string.IsNullOrEmpty(smallImageKey))
            {
                smallFields = $@",""small_image"":""{smallImageKey}"",""small_text"":""{smallImageText}""";
            }

            assets = $@"""assets"":{{
                ""large_image"":""{largeImageKey}"",
                ""large_text"":""{largeImageText}""
                {smallFields}
            }},";
        }

        // Build party block
        string party = "";
        if (partyMax > 0 && !string.IsNullOrEmpty(partyId))
        {
            party = $@"""party"":{{
                ""id"":""{partyId}"",
                ""size"":[{partySize},{partyMax}]
            }},";
        }

        // Build secrets block
        string secrets = "";
        if (!string.IsNullOrEmpty(joinSecret))
        {
            secrets = $@"""secrets"":{{""join"":""{joinSecret}""}},";
        }

        string nonce = Guid.NewGuid().ToString();
        int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        string payload = $@"{{
            ""cmd"":""SET_ACTIVITY"",
            ""args"":{{
                ""pid"":{pid},
                ""activity"":{{
                    ""state"":""{EscapeJson(state)}"",
                    ""details"":""{EscapeJson(details)}"",
                    {assets}
                    {party}
                    {secrets}
                    ""timestamps"":{{""start"":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}
                }}
            }},
            ""nonce"":""{nonce}""
        }}";

        try
        {
            Write(1, payload);
            Read();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RPC] Failed to send presence: {e.Message}");
            Connected = false;
        }
    }

    public void ClearPresence()
    {
        if (!Connected) return;

        string nonce = Guid.NewGuid().ToString();
        int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        string payload = $@"{{""cmd"":""SET_ACTIVITY"",""args"":{{""pid"":{pid},""activity"":null}},""nonce"":""{nonce}""}}";

        try { Write(1, payload); Read(); }
        catch { Connected = false; }
    }

    private void Write(int opcode, string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        byte[] buf = new byte[8 + data.Length];
        BitConverter.GetBytes(opcode).CopyTo(buf, 0);
        BitConverter.GetBytes(data.Length).CopyTo(buf, 4);
        data.CopyTo(buf, 8);
        pipe.Write(buf, 0, buf.Length);
        pipe.Flush();
    }

    private string Read()
    {
        byte[] header = new byte[8];
        pipe.Read(header, 0, 8);
        int length = BitConverter.ToInt32(header, 4);
        if (length <= 0) return "";
        byte[] body = new byte[length];
        pipe.Read(body, 0, length);
        string response = Encoding.UTF8.GetString(body);
        Debug.Log($"[RPC] Discord response: {response}"); // add this
        return response;
    }

    private string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void OnDestroy()
    {
        pipe?.Dispose();
        Connected = false;
    }
}