﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#if !DEBUG_ASSETLENS
#pragma warning disable CS0168
#endif

namespace AssetLens.Reference
{
	internal class RefData
	{
		private const string UNITY_DEFAULT_RESOURCE = "0000000000000000e000000000000000";
		private const string UNITY_BUILTIN_EXTRA    = "0000000000000000f000000000000000";
		
		/// <summary>
		/// from file name
		/// </summary>
		public string guid;
		
		/// <summary>
		/// from unity object type
		/// </summary>
		public string objectType;
		
		/// <summary>
		/// from unity object name
		/// </summary>
		public string objectName;

		/// <summary>
		/// from asset database path
		/// </summary>
		public string objectPath;

		public List<string> ownGuids = new List<string>();
		public List<string> referedByGuids = new List<string>();

		private Version version;

		public uint GetVersion()
		{
			return version;
		}

		public string GetVersionText()
		{
			return version.ToString();
		}

		public RefData(string guid, uint version)
		{
			this.guid = guid;
			this.version = version;

			try
			{
				objectPath = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrWhiteSpace(objectPath))
				{
					objectType = "INVALID";
					objectPath = "NO_PATH_DATA";
					objectName = "NO_PATH_DATA";
				}
				else
				{
					Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(objectPath);
				
					objectType = obj == null ? "NULL" : obj.GetType().FullName;
					objectName = obj == null ? "NULL" : obj.name;
				}	
			}
			catch (Exception e)
			{
#if DEBUG_ASSETLENS
				Debug.LogException(e);
#endif
			}
		}

		public void Save()
		{
			ReferenceSerializer.Serialize(this);
		}

		public void Remove()
		{
			string path = FileSystem.ReferenceCacheDirectory + $"/{guid}.ref";
			File.Delete(path);
		}

		public bool IsBuiltInExtra()
		{
			return guid == UNITY_BUILTIN_EXTRA;
		}

		public bool IsDefaultResource()
		{
			return guid == UNITY_DEFAULT_RESOURCE;
		}

		public static RefData Get(string guid)
		{
			string path = FileSystem.ReferenceCacheDirectory + $"/{guid}.ref";
			
			if (!File.Exists(path))
			{
				// 없으면 새로 만듦
				return New(guid);
			}

			return ReferenceSerializer.Deseriallize(guid);
		}

		public static RefData New(string guid)
		{
			RefData asset = new RefData(guid, Setting.INDEX_VERSION);

			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			string assetContent = File.ReadAllText(assetPath);

			List<string> owningGuids = ReferenceUtil.ParseOwnGuids(assetContent);

			// 보유한 에셋에다 레퍼런스 밀어넣기
			foreach (string owningGuid in owningGuids)
			{
				if (Exist(owningGuid))
				{
					RefData ownAsset = Get(owningGuid);
					if (!ownAsset.referedByGuids.Contains(guid))
					{
						ownAsset.referedByGuids.Add(guid);
						ownAsset.Save();
					}
				}
				else
				{
					if (owningGuid == UNITY_BUILTIN_EXTRA || owningGuid == UNITY_DEFAULT_RESOURCE)
					{
						RefData builtinExtra = new RefData(owningGuid, Setting.INDEX_VERSION);
						
						builtinExtra.referedByGuids.Add(guid);
						builtinExtra.Save();
					}
					else
					{
						RefData ownAsset = New(owningGuid);
						ownAsset.referedByGuids.Add(guid);
						ownAsset.Save();
					}
				}
			}

			asset.ownGuids = owningGuids;

			return asset;
		}

		public static bool Exist(string guid)
		{
			string path = FileSystem.ReferenceCacheDirectory + $"/{guid}.ref";
			return File.Exists(path);
		}
	}
}

#if !DEBUG_ASSETLENS
#pragma warning restore CS0168
#endif