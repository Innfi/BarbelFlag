﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BarbelFlag
{
    public enum TeamFaction
    {
        None = 0,
        Ciri = 1,
        Eredin = 2
    };

    public class Team
    {
        public class Initializer
        {
            public GlobalSetting Setting;
            public GameInstance Game;
            public TeamFaction Faction;
        }

        public TeamFaction Faction { get; protected set; }
        public Dictionary<int, CharacterBase> Members { get; protected set; }

        private GlobalSetting globalSetting;
        private GameInstance gameInstance;


        public Team(Initializer initializer)
        {
            globalSetting = initializer.Setting;
            gameInstance = initializer.Game;
            Faction = initializer.Faction;
            Members = new Dictionary<int, CharacterBase>();
        }

        public int Score { get; private set; }

        public void AddMember(int userId, CharacterBase character)
        {
            Members.Add(userId, character);
        }


        public void RaiseScoreDummy()
        {
            Score = globalSetting.WinScore;
            gameInstance.DummyStatusChanger();
        }
    }

    public class Flag
    {
        public enum FlagCaptureStatus
        {
            Initial = 0,
            Capturing = 5,
            Captured = 10
        }

        public int FlagId { get; protected set; }
        public FlagCaptureStatus CaptureStatus { get; protected set; }
        public TeamFaction OwnerTeamFaction { get; set; }
        public int Score { get; protected set; }
        protected int tickLimit;
        protected int tickCurrent;

        public Flag(int flagId, int limit)
        {
            FlagId = flagId;
            OwnerTeamFaction = TeamFaction.None;
            tickLimit = limit;
            tickCurrent = 0;
        }

        public void StartCapture(TeamFaction faction)
        {
            CaptureStatus = Flag.FlagCaptureStatus.Capturing;
            OwnerTeamFaction = faction;
        }

        public void Tick()
        {
            tickCurrent += 6000;
            if (CaptureStatus == FlagCaptureStatus.Capturing && 
                tickCurrent >= tickLimit)
            {
                CaptureStatus = FlagCaptureStatus.Captured;
                tickCurrent = 0;
            }
        }

        public void GenScore()
        {
            Score = 10;
        }
    }

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
