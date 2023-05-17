using System.ComponentModel.DataAnnotations;

namespace WebAPIServer.ReqRes;

public class SelectChatLobbyRequest : UserAuthRequest
{
    public Int64 UserId { get; set; }

    [Range(1, 100)]
    public Int64 LobbyNum { get; set; }
}

public class SelectChatLobbyResponse
{
    public ErrorCode Result { get; set; } = ErrorCode.None;
    public List<string> ChatHistory { get; set; }
}