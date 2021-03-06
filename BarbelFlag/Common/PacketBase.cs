﻿

namespace BarbelFlag
{
    public enum MessageType
    {
        //None = 0,
        InitCharacter = 1,
        LoadTeam = 2,
        GetFlagViews = 3,
        StartCapture = 4,
        AddScore = 5,
        MoveCharacter = 6,
        AddGameClient = 100,
    }

    public enum ErrorCode
    {
        Ok = 0,
        UserAlreadyRegistered = 11,
        TeamMemberCountLimit = 12,
        GameClientNotExist = 13,
        InvalidUserId = 14,
        GameNotStarted = 100,
        GameEnd = 200,        
        InvalidMessageType = 999
    }

    public class MessageBase
    {
        public MessageType MsgType { get; protected set; }
        public int SenderUserId;
    }

    public class AnswerBase
    {
        public MessageType MsgType { get; protected set; }
        public ErrorCode Code;
    }
}
