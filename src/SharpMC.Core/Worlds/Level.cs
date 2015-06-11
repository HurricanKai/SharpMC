﻿// Distrubuted under the MIT license
// ===================================================
// SharpMC uses the permissive MIT license.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the “Software”), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software
// 
// THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// ©Copyright Kenny van Vulpen - 2015

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using SharpMC.Core.Blocks;
using SharpMC.Core.Entity;
using SharpMC.Core.Enums;
using SharpMC.Core.Interfaces;
using SharpMC.Core.Networking.Packages;
using SharpMC.Core.Utils;

namespace SharpMC.Core.Worlds
{
	public class Level
	{
		public Level()
		{
			CurrentWorldTime = 1200;
			Day = 1;
			OnlinePlayers = new List<Player>();
			DefaultGamemode = Config.GetProperty("Gamemode", Gamemode.Survival);
			BlockWithTicks = new ConcurrentDictionary<Vector3, int>();
			StartTimeOfDayTimer();
			Entities = new List<Entity.Entity>();
			Dimension = 0;
            Timetorain = Globals.Rand.Next(24000, 96000);
		}

		internal int Dimension { get; set; }
		internal string LvlName { get; set; }
		internal int Difficulty { get; set; }
		internal Gamemode DefaultGamemode { get; set; }
		internal LvlType LevelType { get; set; }
		internal List<Player> OnlinePlayers { get; private set; }
		internal int CurrentWorldTime { get; set; }
		internal int Day { get; set; }
		public WorldProvider Generator { get; set; }
		internal List<Entity.Entity> Entities { get; private set; }
		internal ConcurrentDictionary<Vector3, int> BlockWithTicks { get; private set; }
        public int Timetorain { get; set; }
        public bool Raining { get; set; }

		#region APISpecific

		public Player[] GetOnlinePlayers
		{
			get { return OnlinePlayers.ToArray(); }
		}

		#endregion

		public void RemovePlayer(Player player)
		{
			lock (OnlinePlayers)
			{
				OnlinePlayers.Remove(player);
			}
		}

		public Player GetPlayer(int entityId)
		{
			foreach (var p in OnlinePlayers)
			{
				if (p.EntityId == entityId) return p;
			}
			return null;
		}

		public void AddPlayer(Player player)
		{
			OnlinePlayers.Add(player);

			new PlayerListItem(player.Wrapper)
			{
				Action = 0,
				Gamemode = player.Gamemode,
				Username = player.Username,
				Uuid = player.Uuid
			}.Broadcast(this); //Send playerlist item to old players & player him self

			BroadcastExistingPlayers(player.Wrapper);
		}

		public void BroadcastChat(string message)
		{
			foreach (var i in OnlinePlayers)
			{
				new ChatMessage(i.Wrapper) {Message = @message}.Write();
			}
		}
        public void BroadcastChat(string message, Player sender)
        {
            foreach (var i in OnlinePlayers)
            {
                if (i == sender)
                {
                    continue;
                }
                new ChatMessage(i.Wrapper) { Message = @message }.Write();
            }
        }

		internal void BroadcastExistingPlayers(ClientWrapper caller)
		{
			foreach (var i in OnlinePlayers)
			{
				if (i.Wrapper != caller)
				{
					new PlayerListItem(caller)
					{
						Action = 0,
						Gamemode = i.Gamemode,
						Username = i.Username,
						Uuid = i.Uuid
					}.Write(); //Send TAB Item
					new SpawnPlayer(caller) {Player = i}.Write(); //Spawn the old player to new player
					new SpawnPlayer(i.Wrapper) {Player = caller.Player}.Write(); //Spawn the new player to old player
					i.BroadcastEquipment();
				}
			}
		}

		internal void BroadcastPlayerRemoval(ClientWrapper caller)
		{
			new PlayerListItem(caller)
			{
				Action = 0,
				Gamemode = caller.Player.Gamemode,
				Username = caller.Player.Username,
				Uuid = caller.Player.Uuid
			}.Broadcast(this);
		}

		public void SaveChunks()
		{
			Generator.SaveChunks(LvlName);
		}

		private int Mod(double val)
		{
			return (int)(((val%16) + 16)%16);
		}

		public Block GetBlock(Vector3 blockCoordinates)
		{
			Vector2 chunkcoords = new Vector2((int) blockCoordinates.X >> 4, (int) blockCoordinates.Z >> 4);
			var chunk = Generator.GenerateChunkColumn(chunkcoords);

			var bid = chunk.GetBlock(Mod(blockCoordinates.X), (int) blockCoordinates.Y,
				Mod(blockCoordinates.Z));

			var metadata = chunk.GetMetadata(Mod(blockCoordinates.X), (int)blockCoordinates.Y,
				Mod(blockCoordinates.Z));

			var block = BlockFactory.GetBlockById(bid, metadata);
			block.Coordinates = blockCoordinates;
			block.Metadata = metadata;

			return block;
		}

		public void SetBlock(Vector3 coordinates, Block block)
		{
			block.Coordinates = coordinates;
			SetBlock(block);
		}

		public void SetBlock(Block block, bool broadcast = true, bool applyPhysics = true)
		{
			var chunk =
				Generator.GenerateChunkColumn(new ChunkCoordinates((int) block.Coordinates.X >> 4, (int) block.Coordinates.Z >> 4));
			chunk.SetBlock(Mod(block.Coordinates.X), (int)block.Coordinates.Y,
				Mod(block.Coordinates.Z),
				block);
			chunk.IsDirty = true;

			if (applyPhysics) ApplyPhysics((int) block.Coordinates.X, (int) block.Coordinates.Y, (int) block.Coordinates.Z);

			if (!broadcast) return;
			BlockChange.Broadcast(block, this);
		}

		internal void ApplyPhysics(int x, int y, int z)
		{
			DoPhysics(x - 1, y, z);
			DoPhysics(x + 1, y, z);
			DoPhysics(x, y - 1, z);
			DoPhysics(x, y + 1, z);
			DoPhysics(x, y, z - 1);
			DoPhysics(x, y, z + 1);
		}

		private void DoPhysics(int x, int y, int z)
		{
			var block = GetBlock(new Vector3(x, y, z));
			if (block is BlockAir) return;
			block.DoPhysics(this);
		}

		public void ScheduleBlockTick(Block block, int tickRate)
		{
			BlockWithTicks[block.Coordinates] = CurrentWorldTime + tickRate;
		}

		public void AddEntity(Entity.Entity entity)
		{
			Entities.Add(entity);
		}

		public void RemoveEntity(Entity.Entity entity)
		{
			if (Entities.Contains(entity)) Entities.Remove(entity);
		}

		public PlayerLocation GetSpawnPoint()
		{
			var spawn = Generator.GetSpawnPoint();
			return new PlayerLocation(spawn.X, spawn.Y, spawn.Z);
		}

		#region TickTimer

		private Task _gameTickThread;

		internal void StartTimeOfDayTimer()
		{
			_gameTickThread = new Task(StartTickTimer);
			_gameTickThread.Start();
		}


		private static readonly Timer KtTimer = new Timer();

		private void StartTickTimer()
		{
			KtTimer.Elapsed += GameTick;
			KtTimer.Interval = 50;
			KtTimer.Start();
		}

		private void DayTick()
		{
			if (CurrentWorldTime < 24000)
			{
				CurrentWorldTime += 1;
			}
			else
			{
				CurrentWorldTime = 0;
				Day++;
			}

			lock (OnlinePlayers)
			{
				foreach (var i in OnlinePlayers)
				{
					new TimeUpdate(i.Wrapper) {Time = CurrentWorldTime, Day = Day}.Write();
				}
			}
		}

		private readonly Stopwatch _sw = new Stopwatch();
		private long _lastCalc;

		public int CalculateTps(Player player = null)
		{
			var average = _lastCalc;

			var d = 1000 - _lastCalc;
			d = d/50;
			var exact = d;

			var color = "a";
			if (exact <= 10) color = "c";
			if (exact <= 15 && exact > 10) color = "e";


			if (player != null)
			{
				player.SendChat("TPS: §" + color + exact);
				player.SendChat("Miliseconds in Tick: " + average + "ms");
			}

			return (int) exact;
		}

        private void WeatherTick()
        {
            if(Timetorain == 0 && !Raining)
            {
                Raining = true;
                foreach (var player in OnlinePlayers.ToArray())
                {
                    new ChangeGameState(player.Wrapper)
                    {
                        Reason = GameStateReason.BeginRaining,
                        Value = (float)1
                    }.Write();
                }
                Timetorain = Globals.Rand.Next(12000, 36000);
            }
            else if(!Raining)
            {
                --Timetorain;
            }
            else if(Raining && Timetorain == 0)
            {
                Raining = false;
                foreach (var player in OnlinePlayers.ToArray())
                {
                    new ChangeGameState(player.Wrapper)
                    {
                        Reason = GameStateReason.EndRaining,
                        Value = (float)1
                    }.Write();
                }
                Timetorain = Globals.Rand.Next(24000, 96000);
            }
            else if(Raining)
            {
                --Timetorain;
            }
        }

		private int _saveTick;

		private void GameTick(object source, ElapsedEventArgs e)
		{
			_sw.Start();

			DayTick();

            WeatherTick();

			foreach (var blockEvent in BlockWithTicks.ToArray())
			{
				if (blockEvent.Value <= CurrentWorldTime)
				{
					GetBlock(blockEvent.Key).OnTick(this);
					int value;
					BlockWithTicks.TryRemove(blockEvent.Key, out value);
				}
			}

			foreach (var player in OnlinePlayers.ToArray())
			{
				player.OnTick();
			}

			foreach (var entity in Entities.ToArray())
			{
				entity.OnTick();
			}

			if (_saveTick == 3000)
			{
				_saveTick = 0;
				ConsoleFunctions.WriteInfoLine("Saving chunks");
				var sw = new Stopwatch();
				sw.Start();
				SaveChunks();
				sw.Stop();
				ConsoleFunctions.WriteInfoLine("Saving chunks took: " + sw.ElapsedMilliseconds + "MS");

				GC.Collect(); //Collect garbage
			}
			else
			{
				_saveTick++;
			}

			if (_saveTick == 750) GC.Collect();

			_sw.Stop();
			_lastCalc = _sw.ElapsedMilliseconds;
			_sw.Reset();
		}

		#endregion

        public int GetWorldTime()
        {
            return CurrentWorldTime;
        }

        public void SetWorldTime(int time)
        {
            CurrentWorldTime = time;
        }
	}
}