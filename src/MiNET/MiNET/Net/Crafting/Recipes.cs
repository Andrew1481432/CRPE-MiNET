﻿#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE. 
// The License is based on the Mozilla Public License Version 1.1, but Sections 14 
// and 15 have been added to cover use of software over a computer network and 
// provide for limited attribution for the Original Developer. In addition, Exhibit A has 
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is MiNET.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2018 Niclas Olofsson. 
// All Rights Reserved.

#endregion

using System;
using System.Collections.Generic;
using MiNET.Items;
using MiNET.Utils;

namespace MiNET.Net.Crafting
{
	public class Recipes : List<Recipe>, IPacketDataObject
	{
		public void Write(Packet packet)
		{
			packet.WriteUnsignedVarInt((uint) Count);

			foreach (var recipe in this)
			{
				recipe.Write(packet);
			}
		}

		public static Recipes Read(Packet packet)
		{
			var recipes = new Recipes();

			var count = packet.ReadUnsignedVarInt();
			for (var i = 0; i < count; i++)
			{
				recipes.Add(Recipe.Read(packet));
			}

			return recipes;
		}
	}

	public abstract class Recipe : IPacketDataObject
	{
		public abstract RecipeType Type { get; }

		public UUID Id { get; set; } = new UUID(Guid.NewGuid().ToString());
		public string Block { get; set; }
		public int Priority { get; set; }

		public void Write(Packet packet)
		{
			packet.WriteSignedVarInt((int) Type);
			WriteData(packet);
		}

		protected virtual void WriteData(Packet packet) { }

		public static Recipe Read(Packet packet)
		{
			var type = (RecipeType) packet.ReadSignedVarInt();

			return type switch
			{
				RecipeType.Shapeless or RecipeType.ShalepessChemistry or RecipeType.ShulkerBox => ShapelessRecipe.ReadData(packet),
				RecipeType.Shaped or RecipeType.ShapedChemistry => ShapedRecipe.ReadData(packet),
				RecipeType.Furnace => SmeltingRecipe.ReadData(packet),
				RecipeType.FurnaceData => SmeltingDataRecipe.ReadData(packet),
				RecipeType.Multi => MultiRecipe.ReadData(packet),
				//RecipeType.ShulkerBox => ShulkerBoxRecipe.ReadData(packet),
				//RecipeType.ShalepessChemistry => ShalepessChemistryRecipe.ReadData(packet),
				//RecipeType.ShapedChemistry => ShapedChemistryRecipe.ReadData(packet),
				RecipeType.SmithingTransform => SmithingTransformRecipe.ReadData(packet),
				RecipeType.SmithingTrim => SmithingTrimRecipe.ReadData(packet),
				_ => throw new ArgumentException($"Unexpected recipe type [{type}]")
			};
		}
	}

	/// <summary>
	/// These are recipe keys to indicate special recipe actions that doesn't
	/// fit into normal recipes.
	/// </summary>
	public class MultiRecipe : Recipe
	{
		public override RecipeType Type => RecipeType.Multi;

		// From PMMP
		//public const TYPE_REPAIR_ITEM = "00000000-0000-0000-0000-000000000001";
		//public const TYPE_MAP_EXTENDING = "D392B075-4BA1-40AE-8789-AF868D56F6CE";
		//public const TYPE_MAP_EXTENDING_CARTOGRAPHY = "8B36268C-1829-483C-A0F1-993B7156A8F2";
		//public const TYPE_MAP_CLONING = "85939755-BA10-4D9D-A4CC-EFB7A8E943C4";
		//public const TYPE_MAP_CLONING_CARTOGRAPHY = "442D85ED-8272-4543-A6F1-418F90DED05D";
		//public const TYPE_MAP_UPGRADING = "AECD2294-4B94-434B-8667-4499BB2C9327";
		//public const TYPE_MAP_UPGRADING_CARTOGRAPHY = "98C84B38-1085-46BD-B1CE-DD38C159E6CC";
		//public const TYPE_BOOK_CLONING = "D1CA6B84-338E-4F2F-9C6B-76CC8B4BD98D";
		//public const TYPE_BANNER_DUPLICATE = "B5C5D105-75A2-4076-AF2B-923EA2BF4BF0";
		//public const TYPE_BANNER_ADD_PATTERN = "D81AAEAF-E172-4440-9225-868DF030D27B";
		//public const TYPE_FIREWORKS = "00000000-0000-0000-0000-000000000002";
		//public const TYPE_MAP_LOCKING_CARTOGRAPHY = "602234E4-CAC1-4353-8BB7-B1EBFF70024B";

		public int UniqueId { get; set; }

		protected override void WriteData(Packet packet)
		{
			packet.Write(Id);
			packet.WriteVarInt(UniqueId);
		}

		internal static Recipe ReadData(Packet packet)
		{
			return new MultiRecipe()
			{
				Id = packet.ReadUUID(),
				UniqueId = packet.ReadVarInt()
			};
		}
	}

	public class ShapelessRecipe : Recipe
	{
		public override RecipeType Type => RecipeType.Shapeless;

		public int UniqueId { get; set; }
		public List<RecipeIngredient> Input { get; private set; }
		public List<Item> Result { get; private set; }

		public ShapelessRecipe()
		{
			Input = new List<RecipeIngredient>();
			Result = new List<Item>();
		}

		public ShapelessRecipe(List<Item> result, List<RecipeIngredient> input, string block = null) : this()
		{
			Result = result;
			Input = input;
			Block = block;
		}

		public ShapelessRecipe(Item result, List<RecipeIngredient> input, string block = null) : this()
		{
			Result.Add(result);
			Input = input;
			Block = block;
		}

		protected override void WriteData(Packet packet)
		{
			packet.Write(Id.ToString());

			packet.WriteUnsignedVarInt((uint) Input.Count);
			foreach (var ingredient in Input)
			{
				packet.Write(ingredient);
			}

			packet.WriteUnsignedVarInt((uint) Result.Count);
			foreach (Item item in Result)
			{
				packet.Write(item, false);
			}

			packet.Write(Id);
			packet.Write(Block);
			packet.WriteSignedVarInt(Priority);
			packet.WriteVarInt(UniqueId);
		}

		internal static Recipe ReadData(Packet packet)
		{
			packet.ReadString(); // some unique id

			var recipe = new ShapelessRecipe();

			var inputCount = packet.ReadUnsignedVarInt();
			for (var i = 0; i < inputCount; i++)
			{
				recipe.Input.Add(RecipeIngredient.Read(packet));
			}

			var outputCount = packet.ReadUnsignedVarInt();
			for (var i = 0; i < outputCount; i++)
			{
				recipe.Result.Add(packet.ReadItem(false));
			}

			recipe.Id = packet.ReadUUID();
			recipe.Block = packet.ReadString();
			recipe.Priority = packet.ReadSignedVarInt();
			recipe.UniqueId = packet.ReadVarInt();

			return recipe;
		}
	}

	public class ShapedRecipe : Recipe
	{
		public override RecipeType Type => RecipeType.Shaped;

		public int UniqueId { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public RecipeIngredient[] Input { get; set; }
		public List<Item> Result { get; set; }

		public ShapedRecipe(int width, int height)
		{
			Width = width;
			Height = height;
			Input = new RecipeIngredient[Width * height];
			Result = new List<Item>();
		}

		public ShapedRecipe(int width, int height, Item result, RecipeIngredient[] input, string block = null) : this(width, height)
		{
			Result.Add(result);
			Input = input;
			Block = block;
		}

		public ShapedRecipe(int width, int height, List<Item> result, RecipeIngredient[] input, string block = null) : this(width, height)
		{
			Result = result;
			Input = input;
			Block = block;
		}

		protected override void WriteData(Packet packet)
		{
			packet.Write(Id.ToString());

			packet.WriteSignedVarInt(Width);
			packet.WriteSignedVarInt(Height);
			for (int h = 0; h < Height; h++)
			{
				for (int w = 0; w < Width; w++)
				{
					packet.Write(Input[(h * Width) + w]);
				}
			}

			packet.WriteUnsignedVarInt((uint) Result.Count);
			foreach (var item in Result)
			{
				packet.Write(item, false);
			}

			packet.Write(Id);
			packet.Write(Block);
			packet.WriteSignedVarInt(Priority);
			packet.WriteVarInt(UniqueId);
		}

		internal static Recipe ReadData(Packet packet)
		{
			packet.ReadString(); // some unique id

			var recipe = new ShapedRecipe(
				width: packet.ReadSignedVarInt(),
				height: packet.ReadSignedVarInt());

			for (int h = 0; h < recipe.Height; h++)
			{
				for (int w = 0; w < recipe.Width; w++)
				{
					recipe.Input[(h * recipe.Width) + w] = RecipeIngredient.Read(packet);
				}
			}

			var outputCount = packet.ReadUnsignedVarInt();
			for (var i = 0; i < outputCount; i++)
			{
				recipe.Result.Add(packet.ReadItem(false));
			}

			recipe.Id = packet.ReadUUID();
			recipe.Block = packet.ReadString();
			recipe.Priority = packet.ReadSignedVarInt();
			recipe.UniqueId = packet.ReadVarInt();

			return recipe;
		}
	}

	public class SmeltingRecipe : SmeltingRecipeBase
	{
		public override RecipeType Type => RecipeType.Furnace;

		internal static Recipe ReadData(Packet packet)
		{
			var recipe = new SmeltingRecipe();

			var id = packet.ReadSignedVarInt();

			recipe.Input = ItemFactory.GetItem(id);

			return ReadData(packet, recipe);
		}
	}

	public class SmeltingDataRecipe : SmeltingRecipeBase
	{
		public override RecipeType Type => RecipeType.FurnaceData;
		
		internal static Recipe ReadData(Packet packet)
		{
			var recipe = new SmeltingDataRecipe();

			var id = packet.ReadSignedVarInt();
			var metadata = packet.ReadSignedVarInt();

			recipe.Input = ItemFactory.GetItem(id, (short) metadata);

			return ReadData(packet, recipe);
		}
	}

	public abstract class SmeltingRecipeBase : Recipe
	{
		public Item Input { get; set; }
		public Item Result { get; set; }

		protected SmeltingRecipeBase()
		{
		}

		public SmeltingRecipeBase(Item result, Item input, string block = null) : this()
		{
			Result = result;
			Input = input;
			Block = block;
		}

		protected override void WriteData(Packet packet)
		{
			packet.WriteSignedVarInt(Input.RuntimeId);
			if (Type == RecipeType.FurnaceData)
			{
				packet.WriteSignedVarInt(Input.Metadata);
			}

			packet.Write(Result, false);
			packet.Write(Block);
		}

		protected static Recipe ReadData(Packet packet, SmeltingRecipeBase recipe)
		{
			recipe.Result = packet.ReadItem(false);
			recipe.Block = packet.ReadString();

			return recipe;
		}
	}

	public class SmithingTransformRecipe : Recipe
	{
		public override RecipeType Type => RecipeType.SmithingTransform;

		public int UniqueId { get; set; }
		public RecipeIngredient Template { get; set; }
		public RecipeIngredient Input { get; set; }
		public RecipeIngredient Addition { get; set; }
		public Item Output { get; set; }

		public SmithingTransformRecipe()
		{
		}

		public SmithingTransformRecipe(Item output, RecipeIngredient template, RecipeIngredient input, RecipeIngredient addition, string block = null) : this()
		{
			Output = output;
			Template = template;
			Input = input;
			Addition = addition;
			Block = block;
		}

		protected override void WriteData(Packet packet)
		{
			packet.Write(Id.ToString());

			packet.Write(Template);
			packet.Write(Input);
			packet.Write(Addition);
			packet.Write(Output, false);
			packet.Write(Block);
			packet.WriteVarInt(UniqueId);
		}

		internal static Recipe ReadData(Packet packet)
		{
			packet.ReadString(); // some unique id

			return new SmithingTransformRecipe()
			{
				Template = RecipeIngredient.Read(packet),
				Input = RecipeIngredient.Read(packet),
				Addition = RecipeIngredient.Read(packet),
				Output = packet.ReadItem(false),
				Block = packet.ReadString(),
				UniqueId = packet.ReadVarInt()
			};
		}
	}

	public class SmithingTrimRecipe : Recipe
	{
		public override RecipeType Type => RecipeType.SmithingTrim;

		public int UniqueId { get; set; }
		public RecipeIngredient Template { get; set; }
		public RecipeIngredient Input { get; set; }
		public RecipeIngredient Addition { get; set; }

		public SmithingTrimRecipe()
		{
		}

		public SmithingTrimRecipe(RecipeIngredient template, RecipeIngredient input, RecipeIngredient addition, string block = null) : this()
		{
			Template = template;
			Input = input;
			Addition = addition;
			Block = block;
		}

		protected override void WriteData(Packet packet)
		{
			packet.Write(Id.ToString());

			packet.Write(Template);
			packet.Write(Input);
			packet.Write(Addition);
			packet.Write(Block);
			packet.WriteSignedVarInt(UniqueId);
		}

		internal static Recipe ReadData(Packet packet)
		{
			packet.ReadString(); // some unique id

			return new SmithingTrimRecipe()
			{
				Template = RecipeIngredient.Read(packet),
				Input = RecipeIngredient.Read(packet),
				Addition = RecipeIngredient.Read(packet),
				Block = packet.ReadString(),
				UniqueId = packet.ReadSignedVarInt()
			};
		}
	}

	public class PotionContainerChangeRecipe
	{
		public int Input { get; set; }
		public int Ingredient { get; set; }
		public int Output { get; set; }
	}

	public class PotionTypeRecipe
	{
		public int Input { get; set; }
		public int InputMeta { get; set; }
		public int Ingredient { get; set; }
		public int IngredientMeta { get; set; }
		public int Output { get; set; }
		public int OutputMeta { get; set; }
	}

	public class MaterialReducerRecipe
	{
		public int Input { get; set; }
		public int InputMeta { get; set; }

		public MaterialReducerRecipeOutput[] Output { get; set; }

		public MaterialReducerRecipe()
		{

		}

		public MaterialReducerRecipe(int inputId, int inputMeta, params MaterialReducerRecipeOutput[] outputs)
		{
			Input = inputId;
			InputMeta = inputMeta;

			Output = outputs;
		}

		public class MaterialReducerRecipeOutput
		{
			public int ItemId { get; set; }
			public int ItemCount { get; set; }

			public MaterialReducerRecipeOutput()
			{

			}

			public MaterialReducerRecipeOutput(int itemId, int itemCount)
			{
				ItemId = itemId;
				ItemCount = itemCount;
			}
		}
	}

}