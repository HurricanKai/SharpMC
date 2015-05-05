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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpMC.Blocks;
using SharpMC.Entity;
using SharpMC.Enums;
using SharpMC.Utils;
using SharpMC.Worlds;

namespace SharpMC.Items
{
	public class ItemWaterBucket : Item
	{
		internal ItemWaterBucket() : base(326, 0)
		{
			IsUsable = true;
			MaxStackSize = 1;
		}

		public override void UseItem(Level world, Player player, Vector3 blockCoordinates, BlockFace face)
		{
			int slot = player.Inventory.CurrentSlot + 36;
			player.Inventory.SetSlot(slot, 325, 0, 1); //'Empty' the bucket.
			world.SetBlock(new BlockFlowingWater() {Coordinates = blockCoordinates}, true, true); //Place the water
			world.GetBlock(blockCoordinates).OnTick(world); //Update the water
		}
	}
}