using System;

namespace AQRLiteHelper.Bridge
{
#pragma warning disable CS0649
    [Serializable]
    internal sealed class OpenLevelRequest
    {
        public int levelId;
        public string downloadUrl = string.Empty;
        public string fileName = string.Empty;
    }

    [Serializable]
    internal sealed class HealthResponse
    {
        public bool connected;
        public string modVersion = string.Empty;
        public int port;
        public string scene = string.Empty;
        public bool editorReady;
        public bool busy;
    }
#pragma warning restore CS0649

    [Serializable]
    internal sealed class BridgeResponse
    {
        public bool success;
        public string state = string.Empty;
        public string code = string.Empty;
        public string message = string.Empty;

        public static BridgeResponse Ok(string message, string state = "opening")
        {
            return new BridgeResponse
            {
                success = true,
                state = state,
                message = message
            };
        }

        public static BridgeResponse Fail(string code, string message)
        {
            return new BridgeResponse
            {
                success = false,
                code = code,
                message = message
            };
        }
    }

    internal readonly struct BridgeResult
    {
        public BridgeResult(int statusCode, BridgeResponse body)
        {
            StatusCode = statusCode;
            Body = body;
        }

        public int StatusCode { get; }
        public BridgeResponse Body { get; }

        public static BridgeResult BadRequest(string code, string message)
        {
            return new BridgeResult(400, BridgeResponse.Fail(code, message));
        }

        public static BridgeResult Conflict(string code, string message)
        {
            return new BridgeResult(409, BridgeResponse.Fail(code, message));
        }

        public static BridgeResult Error(string code, string message)
        {
            return new BridgeResult(500, BridgeResponse.Fail(code, message));
        }
    }
}
