namespace WebAPIServer.Util;

public static class GenerateKey
{
    public static string StageUserKey(Int64 userId)
    {
        return new string("User_" + userId + "_Stage");
    }

    public static string StageItemKey(Int64 userId)
    {
        return new string("User_" + userId + "_StageItem");
    }

    public static string StageEnemyKey(Int64 userId)
    {
        return new string("User_" + userId + "_StageEnemy");
    }

    public static string LobbyChatHistoryKey(Int64 lobbyNum)
    {
        return new string("Lobby_" + lobbyNum + "_History");
    }
}