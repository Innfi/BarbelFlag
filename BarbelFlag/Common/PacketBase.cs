﻿

namespace BarbelFlag
{
    public enum MessageType
    {
        //None = 0,
        InitCharacter = 1,
        LoadTeam = 2,
        GetFlagsStatus = 3,
        StartCapture = 4,
    }

    public enum ErrorCode
    {
        Ok = 0,
        UserAlreadyRegistered = 11,
        TeamMemberCountLimit = 12,
        //InvalidMessageType = 999
    }

    public abstract class MessageBase
    {
        public MessageType MsgType { get; protected set; }
    }

    public abstract class AnswerBase
    {
        public MessageType MsgType { get; protected set; }
        public ErrorCode Code;
    }
}