﻿using System.Collections.Generic;
using System.Threading;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BarbelFlag;
using Newtonsoft.Json;
using System.IO;

/*
TODO
--------------------------------------------------
character: status effect parser
character: skill

DONE
--------------------------------------------------
character
- basic stats
flags
- status: neutral / capturing  / captured
- generate score
- flag id
- capture flag by ticks
generate score by flag ticks
- alarm score from flag to game instance by message
refactoring: fix team ids to 2 only. no need to variable ids
init game instance
instantiate chracters with teams
instantiate flags
assign characters to teams to limit (fixed number)
handle events (move, attack, capture)
limit handling character actions until the game starts
start game 
finish game when score reached limit
limit flag ticks after game ends
refactoring: flags ticks from GameInstance.Update()
refactoring: flag status view data
organize Message / Answer from character's view
refactoring: GameInstance
refactoring: HandleMessage to message queue
- async reply answer 
refactoring: game loop tests
refactoring: game loop interface
character: position
character: calculate position within server frame
character: status effect (initial)

*/

namespace CoreTest
{
    [TestClass]
    public class CharacterTest
    {
        [TestMethod]
        public void Test10CharacterMilli()
        {
            CharacterBase milli = new CharacterMilli();

            Assert.AreEqual(milli.Health, 150);
            Assert.AreEqual(milli.AutoRange, 20);
        }

        [TestMethod]
        public void Test11CharacterEnnfi()
        {
            CharacterBase ennfi = new CharacterEnnfi();

            Assert.AreEqual(ennfi.Health, 100);
            Assert.AreEqual(ennfi.AutoRange, 30);
        }

        [TestMethod]
        public void Test12CharacterInnfi()
        {
            CharacterBase innfi = new CharacterInnfi();

            Assert.AreEqual(innfi.Health, 200);
            Assert.AreEqual(innfi.AutoRange, 20);
        }

        [TestMethod]
        public void Test20CharacterFactory()
        {
            //TODO: load basic character stats by factory 
        }
    }

    [TestClass]
    public class GameInstanceTest
    {
        protected GameInstance game;
        protected GlobalSetting globalSetting;

        [TestInitialize]
        public void SetUp()
        {
            game = new GameInstance();
            globalSetting = new GlobalSetting();
        }

        [TestMethod]
        public void Test1InitCharacter()
        {
            var dummyUserId = 1;
            var gameClient = new GameClient(dummyUserId, game.MsgQ);
            game.AddClient(gameClient);

            var message = new MessageInitCharacter
            {
                UserId = dummyUserId,
                CharType = CharacterType.Innfi,
                Faction = TeamFaction.Ciri,
                SenderUserId = dummyUserId
            };

            game.EnqueueMessage(message);
            game.Update();

            var character = gameClient.Character;
            Assert.AreEqual(character.CharType, message.CharType);
        }

        [TestMethod]
        public void Test1InitCharacter1DuplicateUserId()
        {
            var dummyUserId = 1;
            var gameClient = new GameClient(dummyUserId, game.MsgQ);
            game.AddClient(gameClient);

            var message = new MessageInitCharacter
            {
                UserId = 1,
                CharType = CharacterType.Innfi,
                Faction = TeamFaction.Eredin,
                SenderUserId = 1
            };

            game.EnqueueMessage(message);
            game.Update();

            Assert.AreEqual(gameClient.LastAnswer.Code, ErrorCode.Ok);

            game.EnqueueMessage(message);
            game.Update();

            Assert.AreEqual(gameClient.LastAnswer.Code, ErrorCode.UserAlreadyRegistered);
        }

        [TestMethod]
        public void Test1InitCharacter2CheckTeam()
        {
            var gameClient1 = new GameClient(1, game.MsgQ);
            var gameClient2 = new GameClient(2, game.MsgQ);
            game.AddClient(gameClient1);
            game.AddClient(gameClient2);

            var faction = TeamFaction.Ciri;
            gameClient1.SendDummyMessage(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                CharType = CharacterType.Innfi,
                Faction = faction,
                SenderUserId = gameClient1.UserId
            });
            gameClient2.SendDummyMessage(new MessageInitCharacter
            {
                UserId = gameClient2.UserId,
                CharType = CharacterType.Ennfi,
                Faction = faction,
                SenderUserId = gameClient2.UserId
            });
            game.Update();


            gameClient1.SendDummyMessage(new MessageLoadTeam
            {
                Faction = faction,
                SenderUserId = gameClient1.UserId
            });
            game.Update();

            var answer = (AnswerLoadTeam)gameClient1.LastAnswer;
            Assert.AreEqual(answer.Code, ErrorCode.Ok);
            Assert.AreEqual(answer.MsgType, MessageType.LoadTeam);
            var members = answer.TeamMembers;

            Assert.AreEqual(members.ContainsKey(gameClient1.UserId), true);
            Assert.AreEqual(members.ContainsKey(gameClient2.UserId), true);
        }

        [TestMethod]
        public void Test1InitCharacter3TeamMemberFull()
        {
            //FIXME
            var globalSetting = new GlobalSetting
            {
                MemberCount = 5
            };
            var otherGame = new GameInstance(globalSetting);
            var faction = TeamFaction.Ciri;
            AddCharacters(otherGame, faction);

            var invalidUserId = 99;
            var gameClient = new GameClient(invalidUserId, otherGame.MsgQ);
            otherGame.AddClient(gameClient);
            otherGame.EnqueueMessage(new MessageInitCharacter
            {
                UserId = invalidUserId,
                CharType = CharacterType.Innfi,
                Faction = faction,
                SenderUserId = invalidUserId
            });
            otherGame.Update();

            var lastAnswer = gameClient.LastAnswer;
            Assert.AreEqual(lastAnswer.Code, ErrorCode.TeamMemberCountLimit);
        }

        protected void AddCharacters(GameInstance gameInstance, TeamFaction faction)
        {
            for (int i = 1; i < 6; i++)
            {
                gameInstance.AddClient(
                new GameClient(i, gameInstance.MsgQ));

                gameInstance.EnqueueMessage(new MessageInitCharacter
                {
                    UserId = i,
                    Faction = faction,
                    CharType = CharacterType.Innfi,
                    SenderUserId = i
                });

                gameInstance.Update();
            }
        }

        protected List<MessageInitCharacter> GenerateDummyMsgInitChar(TeamFaction faction)
        {
            var messages = new List<MessageInitCharacter>();

            for (int i = 1; i < 6; i++)
            {
                messages.Add(new MessageInitCharacter
                {
                    UserId = i,
                    CharType = CharacterType.Ennfi,
                    Faction = faction
                });
            }

            return messages;
        }

        [TestMethod]
        public void Test1InitFlags1Instantiate()
        {
            game.Reset();
            var gameClient = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient);

            game.EnqueueMessage(new MessageGetFlagViews
            {
                SenderUserId = gameClient.UserId
            });
            game.Update();

            var answer = gameClient.LastAnswer;
            Assert.AreEqual(answer.MsgType, MessageType.GetFlagViews);

            var answerFlagsStatus = (AnswerGetFlagViews)answer;
            Assert.AreEqual(answerFlagsStatus.FlagViews != null, true);

            foreach (var flag in answerFlagsStatus.FlagViews)
            {
                Assert.AreEqual(flag.OwnerTeamFaction, TeamFaction.None);
            }
        }
        
        [TestMethod]
        public void Test2CaptureFlagByHandleMessage()
        {
            game.Reset();
            var gameClient = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient);

            game.Start();

            var flags = LoadFlagViews(gameClient);
            var targetFlag = flags[0];

            game.EnqueueMessage(new MessageStartCapture
            {
                FlagId = targetFlag.FlagId,
                SenderUserId = gameClient.UserId
            });
            game.Update();

            var answer = gameClient.LastAnswer;
            Assert.AreEqual(answer.MsgType, MessageType.StartCapture);
            Assert.AreEqual(answer.Code, ErrorCode.Ok);

            var resultViews = LoadFlagViews(gameClient);
            Assert.AreEqual(resultViews[0].CaptureStatus, Flag.FlagCaptureStatus.Capturing);
        }

        protected List<FlagView> LoadFlagViews(GameClient client)
        {
            game.EnqueueMessage(new MessageGetFlagViews
            {
                SenderUserId = client.UserId
            });
            game.Update();

            var answer = (AnswerGetFlagViews)client.LastAnswer;
            return answer.FlagViews;
        }
        
        [TestMethod]
        public void Test2InitFlags3OwnerTeamID()
        {
            game.Reset();
            var gameClient1 = new GameClient(1, game.MsgQ);
            var gameClient2 = new GameClient(2, game.MsgQ);

            game.AddClient(gameClient1);
            game.AddClient(gameClient2);

            game.Start();

            game.EnqueueMessage(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                CharType = CharacterType.Ennfi,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            game.EnqueueMessage(new MessageInitCharacter
            {
                UserId = gameClient2.UserId,
                CharType = CharacterType.Milli,
                Faction = TeamFaction.Eredin,
                SenderUserId = gameClient2.UserId
            });
            game.Update();

            var flagIndexCiri = 1;
            var flagIndexEredin = 4;
            game.EnqueueMessage(new MessageStartCapture
            {
                FlagId = flagIndexCiri,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            game.EnqueueMessage(new MessageStartCapture
            {
                FlagId = flagIndexEredin,
                Faction = TeamFaction.Eredin,
                SenderUserId = gameClient2.UserId
            });
            game.Update();

            var flagViews = LoadFlagViews(gameClient1);
            Assert.AreEqual(
                flagViews[flagIndexCiri].CaptureStatus, Flag.FlagCaptureStatus.Capturing);
            Assert.AreEqual(
                flagViews[flagIndexCiri].OwnerTeamFaction, TeamFaction.Ciri);

            Assert.AreEqual(
                flagViews[flagIndexEredin].CaptureStatus, Flag.FlagCaptureStatus.Capturing);
            Assert.AreEqual(
                flagViews[flagIndexEredin].OwnerTeamFaction, TeamFaction.Eredin);
        }
        
        [TestMethod]
        public void Test2DoneCaptureByFlagTicks()
        {
            game.Reset();
            var gameClient1 = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient1);

            game.Start();

            game.EnqueueMessage(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                CharType = CharacterType.Ennfi,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            game.Update();

            var index = 1;
            game.EnqueueMessage(new MessageStartCapture
            {
                FlagId = index,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            game.Update();

            var views = LoadFlagViews(gameClient1);
            Assert.AreEqual(views[index].CaptureStatus, Flag.FlagCaptureStatus.Capturing);
            //TODO: ticks by internal handler
            for (var i = 0; i < 10; i++) game.Update();

            views = LoadFlagViews(gameClient1);
            Assert.AreEqual(views[index].CaptureStatus, Flag.FlagCaptureStatus.Captured);
        }
        
        [TestMethod]
        public void Test2GetScoreByTicks()
        {
            game.Reset();
            var gameClient1 = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient1);

            game.Start();

            game.EnqueueMessage(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                CharType = CharacterType.Ennfi,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            game.Update();

            var index = 1;
            game.EnqueueMessage(new MessageStartCapture
            {
                FlagId = index,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            game.Update();

            var views = LoadFlagViews(gameClient1);
            Assert.AreEqual(views[index].CaptureStatus, Flag.FlagCaptureStatus.Capturing);

            for (var i = 0; i < 10; i++) game.Update();
            views = LoadFlagViews(gameClient1);
            Assert.AreEqual(views[index].CaptureStatus, Flag.FlagCaptureStatus.Captured);

            for (var i = 0; i < 10; i++) game.Update();
            game.EnqueueMessage(new MessageLoadTeam
            {
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            game.Update();

            var answerLoadTeam = (AnswerLoadTeam)gameClient1.LastAnswer;
            Assert.AreEqual(answerLoadTeam.Score, 10);
        }

        [TestMethod]
        public void Test3SendRaiseScoreFromFlagToTeam()
        {
            game.Reset();
            var gameClient = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient);

            game.Start();

            var faction = TeamFaction.Eredin;
            var capturedFlag = GetCapturedFlag(gameClient, faction);
            Assert.AreEqual(capturedFlag.OwnerTeamFaction, faction);
            Assert.AreEqual(capturedFlag.CaptureStatus, Flag.FlagCaptureStatus.Captured);

            TickToChangeStatus();
            game.Update();

            game.EnqueDebug(new MessageLoadTeam
            {
                Faction = faction,
                SenderUserId = gameClient.UserId
            });

            var answer = (AnswerLoadTeam)gameClient.LastAnswer;
            Assert.AreEqual(answer.Score, 10);
        }

        protected FlagView GetCapturedFlag(GameClient gameClient, TeamFaction faction)
        {
            var flagId = 1;
            game.EnqueDebug(new MessageStartCapture
            {
                Faction = faction,
                FlagId = flagId
            });
            TickToChangeStatus();
            game.Update();

            game.EnqueDebug(new MessageGetFlagViews
            {
                SenderUserId = gameClient.UserId
            });
            var answer = (AnswerGetFlagViews)gameClient.LastAnswer;

            return answer.FlagViews[flagId];
        }

        protected void TickToChangeStatus()
        {
            for (int i = 0; i < 10; i++) game.Update();
        }
        
        [TestMethod]
        public void Test4DenyStartCaptureBeforeGameStart()
        {
            game.Reset();
            Assert.AreEqual(game.Status, GameStatus.Initial);

            var gameClient1 = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient1);
            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                CharType = CharacterType.Milli,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });
            Assert.AreEqual(gameClient1.LastAnswer.Code, ErrorCode.Ok);


            var msgStartCapture = new MessageStartCapture
            {
                FlagId = 1,
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            };
            game.EnqueDebug(msgStartCapture);
            Assert.AreEqual(gameClient1.LastAnswer.Code, ErrorCode.GameNotStarted);

            game.Start();
            Assert.AreEqual(game.Status, GameStatus.Started);

            game.EnqueDebug(msgStartCapture);

            Assert.AreEqual(gameClient1.LastAnswer.Code, ErrorCode.Ok);
        }
        
        [TestMethod]
        public void Test5FinishGame()
        {
            game.Reset();
            var gameClient1 = new GameClient(1, game.MsgQ);
            var gameClient2 = new GameClient(2, game.MsgQ);
            game.AddClient(gameClient1);
            game.AddClient(gameClient2);
            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                CharType = CharacterType.Milli,
                Faction = TeamFaction.Ciri,
            });

            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient2.UserId,
                CharType = CharacterType.Ennfi,
                Faction = TeamFaction.Eredin
            });

            game.Start();
            

            var faction = TeamFaction.Ciri;
            var flagId = 4;
            CaptureFlag(faction, flagId);

            var views = GetFlagViews(gameClient1);
            Assert.AreEqual(views[flagId].CaptureStatus, Flag.FlagCaptureStatus.Captured);

            GenerateScoreToWin(gameClient2, views[flagId], globalSetting);            
            Assert.AreEqual(game.Status, GameStatus.End);
        }

        protected List<FlagView> GetFlagViews(GameClient gameClient)
        {
            game.EnqueDebug(new MessageGetFlagViews
            {
                SenderUserId = gameClient.UserId
            });

            var answer = (AnswerGetFlagViews)gameClient.LastAnswer;

            return answer.FlagViews;
        }

        protected void CaptureFlag(TeamFaction faction, int flagId)
        {
            game.EnqueDebug(new MessageStartCapture
            {
                Faction = faction,
                FlagId = flagId
            });

            for (int i = 0; i < 10; i++) game.Update();
        }

        protected void GenerateScoreToWin(GameClient gameClient, FlagView view, 
            GlobalSetting globalSetting)
        {
            var score = 0;
            var count = 0;
            while (score < globalSetting.WinScore || count < 20)
            {
                TickToChangeStatus();
                game.Update();

                game.EnqueDebug(new MessageLoadTeam
                {
                    Faction = view.OwnerTeamFaction,
                    SenderUserId = gameClient.UserId
                });

                var answer = (AnswerLoadTeam)gameClient.LastAnswer;


                score = answer.Score;
                count++;
            }

            game.Update();
        }
        
        [TestMethod]
        public void Test5FlagTickBlockedAfterEnd()
        {
            game.Reset();
            var gameClient1 = new GameClient(1, game.MsgQ);
            var gameClient2 = new GameClient(2, game.MsgQ);
            game.AddClient(gameClient1);
            game.AddClient(gameClient2);

            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Ennfi,
                SenderUserId = gameClient1.UserId
            });
            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient2.UserId,
                Faction = TeamFaction.Eredin,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient2.UserId
            });

            game.Start();

            var flagIdCiri = 4;
            var flagIdEredin = 2;
            CaptureFlag(TeamFaction.Ciri, flagIdCiri);
            CaptureFlag(TeamFaction.Eredin, flagIdEredin);

            var flags = GetFlagViews(gameClient1);
            GenerateScoreToWin(gameClient1, flags[flagIdCiri], globalSetting);
            Assert.AreEqual(game.Status, GameStatus.End);

            game.EnqueDebug(new MessageLoadTeam
            {
                Faction = TeamFaction.Eredin,
                SenderUserId = gameClient2.UserId
            });

            var answerLoadTeam = (AnswerLoadTeam)gameClient2.LastAnswer;
            Assert.AreEqual(answerLoadTeam.Score, 90);

            TickToChangeStatus();
            game.Update();

            answerLoadTeam = (AnswerLoadTeam)gameClient2.LastAnswer;
            Assert.AreEqual(answerLoadTeam.Score, 90);
        }
    }

    [TestClass]
    public class GameClientTest
    {
        [TestMethod]
        public void Test1CharacterEmpty()
        {
            var userId = 1;
            var client = new GameClient(userId, new MessageQueue());

            Assert.AreEqual(client.Character == null, true);
        }

        [TestMethod]
        public void Test1InitCharacterToMessageQueue()
        {
            var game = new GameInstance(new GlobalSetting());

            var userId = 1;
            var client = new GameClient(userId, game.MsgQ);
            game.AddClient(client);

            var msgInitChar = new MessageInitCharacter
            {
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Innfi,
                UserId = userId,
                SenderUserId = userId
            };

            client.SendDummyMessage(msgInitChar); //FIXME
            game.Update();

            Assert.AreEqual(client.Character.CharType, msgInitChar.CharType);
        }
    }

    [TestClass]
    public class GameLoopTest
    {
        [TestMethod]
        public void Test1LoopByTask()
        {
            var counter = 0;
            var gameLoop = new GameLoop(() =>
            {
                Thread.Sleep(1);
                return counter++;
            });

            Assert.AreEqual(counter, 0);
            gameLoop.Start();

            Thread.Sleep(1000);
            gameLoop.Stop();
            Assert.AreEqual(counter >= 60 && counter <= 120, true);
        }
    }

    [TestClass]
    public class GameInstanceTest2
    {
        public static GameInstance game;
        public static GameLoop gameLoop;
        public static GameClient specClient;

        [ClassInitialize]
        public static void ClassSetUp(TestContext context)
        {
            game = new GameInstance(new GlobalSetting());
            specClient = new GameClient(999, game.MsgQ);
            game.AddClient(specClient);

            gameLoop = new GameLoop(game.Update);
            gameLoop.Start();
        }

        [ClassCleanup]
        public static void ClassCleanUp()
        {
            gameLoop.Stop();
        }

        [TestMethod]
        public void Test1ReceiveResponseAsync()
        {
            game.Reset();
            var gameClient1 = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient1);

            game.EnqueueMessage(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient1.UserId
            });

            int count;
            for (count = 0; count < 10000; count++)
            {
                if (gameClient1.LastAnswer != null) break;

                Thread.Sleep(1);
            }

            Assert.AreEqual(gameClient1.LastAnswer.MsgType, MessageType.InitCharacter);
        }

        [TestMethod]
        public void Test2ReceiveResponseAsyncCapture()
        {
            game.Reset();
            var gameClient1 = new GameClient(1, game.MsgQ);
            game.AddClient(gameClient1);
            game.Start();

            game.EnqueueMessage(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient1.UserId
            });

            WaitForAnswer(gameClient1);
            gameClient1.ClearLastAnswer();

            game.EnqueueMessage(new MessageStartCapture
            {
                Faction = TeamFaction.Ciri,
                FlagId = 1,
                SenderUserId = gameClient1.UserId
            });

            WaitForAnswer(gameClient1);
            gameClient1.ClearLastAnswer();

            game.EnqueDebug(new MessageGetFlagViews
            {
                SenderUserId = gameClient1.UserId
            });

            var answer = (AnswerGetFlagViews)gameClient1.LastAnswer;
            var flag = answer.FlagViews[1];
            Assert.AreEqual(flag.CaptureStatus, Flag.FlagCaptureStatus.Captured);
            Assert.AreEqual(flag.OwnerTeamFaction, TeamFaction.Ciri);

            game.EnqueDebug(new MessageLoadTeam
            {
                Faction = TeamFaction.Ciri,
                SenderUserId = gameClient1.UserId
            });

            var answerLoadTeam = (AnswerLoadTeam)gameClient1.LastAnswer;
            Assert.AreEqual(answerLoadTeam.Score > 0, true);
        }

        protected int WaitForAnswer(GameClient gameClient)
        {
            int count;

            for (count = 0; count < 2000; count++)
            {
                if (gameClient.LastAnswer != null) break;

                Thread.Sleep(1);
            }

            return count;
        }
    }

    [TestClass]
    public class GameClientTest2
    {
        [TestMethod]
        public void Test0EnqueMessageAddGameClient()
        {
            var game = new GameInstance();
            var gameClient1 = new GameClient(1);

            game.EnqueDebug(new MessageAddGameClient
            {
                gameClient = gameClient1,
                SenderUserId = gameClient1.UserId
            });

            var answer = gameClient1.LastAnswer;
            Assert.AreEqual(answer.Code, ErrorCode.Ok);
        }
    }

    [TestClass]
    public class GameMobileObjectTest
    {
        public GameInstance game;
        public GameClient gameClient1;

        [TestInitialize]
        public void SetUp()
        {
            game = new GameInstance();
            gameClient1 = new GameClient(1);
            game.EnqueDebug(new MessageAddGameClient
            {
                gameClient = gameClient1,
                SenderUserId = gameClient1.UserId
            });
        }

        [TestMethod]
        public void Test0CharacterDefaultPosition()
        {
            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient1.UserId
            });

            Assert.AreEqual(gameClient1.Character != null, true);

            var defaultPos = new ObjectPosition(0, 0, 0);
            Assert.AreEqual(gameClient1.Character.Pos, defaultPos);
        }

        [TestMethod]
        public void Test1CharacterDefaultPositionByFaction()
        {
            var gameClient2 = new GameClient(2);

            game.EnqueDebug(new MessageAddGameClient
            {
                gameClient = gameClient2,
                SenderUserId = gameClient2.UserId
            });

            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient2.UserId,
                Faction = TeamFaction.Eredin,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient2.UserId
            });

            Assert.AreEqual(gameClient2.Character != null, true);

            var defaultPos = new ObjectPosition(999, 999, 999);
            Assert.AreEqual(gameClient2.Character.Pos, defaultPos);
        }

        [TestMethod]
        public void Test2CharacterMove()
        {
            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient1.UserId
            });

            var answer = (AnswerInitCharacter)gameClient1.LastAnswer;
            var targetPos = new ObjectPosition(5.0, 0.0, 5.0);

            game.EnqueDebug(new MessageMoveCharacter
            {
                UserId = gameClient1.UserId,
                BeforePos = answer.Character.Pos,
                TargetPos = targetPos,
                SenderUserId = gameClient1.UserId
            });

            var answerMove = (AnswerMoveCharacter)gameClient1.LastAnswer;
            Assert.AreEqual(answerMove.ResultPos, targetPos);
        }

        [TestMethod]
        public void Test3CharacterMoveLimit()
        {
            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient1.UserId
            });

            var answer = (AnswerInitCharacter)gameClient1.LastAnswer;
            Assert.AreEqual(answer.Character.Pos, new ObjectPosition(0.0f, 0.0f, 0.0f));

            var characterMoveSpeed = answer.Character.MoveSpeed;

            var distantPos = new ObjectPosition(500.0f, 0.0f, 500.0f);
            game.EnqueDebug(new MessageMoveCharacter
            {
                UserId = gameClient1.UserId,
                BeforePos = answer.Character.Pos,
                TargetPos = distantPos,
                SenderUserId = gameClient1.UserId
            });
            var answerMove = (AnswerMoveCharacter)(gameClient1.LastAnswer);
            var resultPos = answerMove.ResultPos;

            Assert.AreEqual(resultPos.PosX < distantPos.PosX, true);
            Assert.AreEqual(resultPos.PosZ < distantPos.PosZ, true);
        }

        [TestMethod]
        public void Test4Character2DMove()
        {
            game.EnqueDebug(new MessageInitCharacter
            {
                UserId = gameClient1.UserId,
                Faction = TeamFaction.Ciri,
                CharType = CharacterType.Innfi,
                SenderUserId = gameClient1.UserId
            });
            var answer = (AnswerInitCharacter)gameClient1.LastAnswer;

            var targetPos2D = new ObjectPosition(5.0f, 5.0f);
            game.EnqueDebug(new MessageMoveCharacter
            {
                UserId = gameClient1.UserId,
                BeforePos = answer.Character.Pos,
                TargetPos = targetPos2D,
                SenderUserId = gameClient1.UserId
            });
            var answerMove = (AnswerMoveCharacter)(gameClient1.LastAnswer);
            var resultPos = answerMove.ResultPos;

            Assert.AreEqual(resultPos, targetPos2D);
        }

        [TestMethod]
        public void Test5CharacterMoveInvalidUserId()
        {
            var invalidUserId = 999;

            var targetPos2D = new ObjectPosition(5.0f, 5.0f);
            game.EnqueDebug(new MessageMoveCharacter
            {
                UserId = invalidUserId,
                BeforePos = new ObjectPosition(0.0f, 0.0f),
                TargetPos = targetPos2D,
                SenderUserId = gameClient1.UserId
            });

            var answerMove = (AnswerMoveCharacter)gameClient1.LastAnswer;
            Assert.AreEqual(answerMove.Code, ErrorCode.InvalidUserId);
        }
    }

    [TestClass]
    public class StatusEffectTest
    {
        [TestMethod]
        public void Test1DamageOverTime()
        {
            var targetCharacter = new CharacterEnnfi();

            var effect = new StatusEffectDoT();
            effect.Target = targetCharacter;
            effect.TickCount = 10;
            effect.TickDamage = 1;
            effect.CurrentTick = 0;

            var healthBefore = targetCharacter.CurrentHealth;
            effect.TakeEffect();

            Assert.AreEqual(targetCharacter.CurrentHealth, 
                healthBefore - effect.TickDamage);
        }

        [TestMethod]
        public void Test1DoTEffectOver()
        {
            var targetCharacter = new CharacterEnnfi();
            var effect = new StatusEffectDoT();
            effect.Target = targetCharacter;
            effect.TickCount = 10;
            effect.TickDamage = 1;
            effect.CurrentTick = 0;

            for (int i = 0; i < effect.TickCount+20; i++) effect.TakeEffect();

            Assert.AreEqual(effect.CurrentTick, effect.TickCount);
            Assert.AreEqual(effect.EffectDone, true);
        }

        [TestMethod]
        public void Test2HealOverTime()
        {
            var targetPlayer = new CharacterInnfi();
            targetPlayer.CurrentHealth = (int)targetPlayer.CurrentHealth / 2;

            var effect = new StatusEffectHoT();
            effect.Target = targetPlayer;
            effect.TickCount = 5;
            effect.TickHeal = 10;
            effect.CurrentTick = 0;

            var healthBefore = targetPlayer.CurrentHealth;

            effect.TakeEffect();
            Assert.AreEqual(targetPlayer.CurrentHealth,
                healthBefore + effect.TickHeal);
        }

        [TestMethod]
        public void Test3AttackDamageBuff()
        {            
            var targetCharacter = new CharacterMilli();
            var ADBefore = targetCharacter.AutoDamage;

            var targets = new List<CharacterBase>();
            targets.Add(targetCharacter);

            var effect = new StatusEffectDamageBuff
            {
                Targets = targets.ToArray(),
                TickCount = 10,
                CurrentTick = 0,
                BuffAmount = 200
            };

            for(int i=0;i<effect.TickCount - 1;i++) effect.TakeEffect();

            Assert.AreEqual(targetCharacter.AutoDamage, ADBefore + effect.BuffAmount);
            effect.TakeEffect();
            Assert.AreEqual(targetCharacter.AutoDamage, ADBefore);
        }

        [TestMethod]
        public void Test4HPBuff()
        {
            var targetCharacter = new CharacterMilli();
            targetCharacter.CurrentHealth = targetCharacter.CurrentHealth / 2;
            var healthBefore = targetCharacter.CurrentHealth;

            var targets = new List<CharacterBase>();
            targets.Add(targetCharacter);

            var effect = new StatusEffectHPBuff
            {
                Targets = targets.ToArray(),
                TickCount = 5,
                CurrentTick = 0,
                BuffAmount = 10
            };
            effect.TakeEffect();

            Assert.AreEqual(targetCharacter.CurrentHealth,
                healthBefore + effect.BuffAmount);
        }

        [TestMethod]
        public void Test4HPBuffExceedinglimit()
        {
            var targetCharacter = new CharacterMilli();
            var healthBefore = targetCharacter.CurrentHealth;

            var targets = new List<CharacterBase>();
            targets.Add(targetCharacter);

            var effect = new StatusEffectHPBuff
            {
                Targets = targets.ToArray(),
                TickCount = 5,
                CurrentTick = 0,
                BuffAmount = 10
            };
            effect.TakeEffect();
            Assert.AreEqual(targetCharacter.CurrentHealth, 
                healthBefore +  effect.BuffAmount);

            for (int i = 0; i < effect.TickCount; i++) effect.TakeEffect();

            Assert.AreEqual(targetCharacter.CurrentHealth, healthBefore);
        }
    }

    [TestClass]
    public class StatusEffectParserTest
    {
        [TestMethod]
        public void Test1ReadDescription()
        {
            var descInstance = new EffectDescription();

            var description = GenerateDummySEDescription();
            var resultInstance =
                JsonConvert.DeserializeObject<EffectDescription>(description);

            Assert.AreEqual(descInstance, resultInstance);
        }

        protected string GenerateDummySEDescription()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                writer.WriteStartObject();

                writer.WritePropertyName("EffectName");
                writer.WriteValue("StatusEffectDoT");
                writer.WritePropertyName("TickCount");
                writer.WriteValue(10);
                writer.WritePropertyName("EffectAmount");
                writer.WriteValue(12);
                writer.WritePropertyName("TargetEntity");
                writer.WriteValue("CharacterBase");
                writer.WritePropertyName("TargetStat");
                writer.WriteValue("Health");
                writer.WritePropertyName("Action");
                writer.WriteValue("Increase");

                writer.WriteEndObject();
            }

            return sb.ToString();
        }

        [TestMethod]
        public void Test2ParseDescription()
        {
            var descriptionJson = GenerateDummySEDescription();
            var parser = new StatusEffectParser();

            var result = parser.Parse(descriptionJson, out var effect);
            Assert.AreEqual(result, true);
            Assert.AreEqual(effect.EffectName, "StatusEffectDoT");
        }
    }

    [TestClass]
    public class SkillTest
    {
        [TestMethod]
        public void Test0InitSkill()
        {
            var skill = new Skill();
        }

        [TestMethod]
        public void Test1DummySkillType()
        {
            var skill = new SkillJumpToPosition();

            Assert.AreEqual(skill.Type, SkillType.JumpToPosition);
        }

        [TestMethod]
        public void Test2DummySkillInvoke()
        {
            var character = new CharacterInnfi();
            character.Pos = new ObjectPosition(0.0f, 0.0f);
            var targetPos = new ObjectPosition(100.0f, 100.0f);

            var skill = new SkillJumpToPosition();
            skill.Invoke(character, targetPos);

            Assert.AreEqual(character.Pos, targetPos);
        }

        [TestMethod]
        public void Test3DummySkillSimpleDamageInvoke()
        {
            var character = new CharacterInnfi();
            Assert.AreEqual(character.Health > 0, true);

            var skill = new SkillSimpleDamage();
            skill.Invoke(character, 1000000);

            Assert.AreEqual(character.Health, 0);
        }
    }
}
