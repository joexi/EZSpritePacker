using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

public class EZSpritePacker : Editor {
	[MenuItem("Tools/Pack")]
	public static void Pack() {
		Object[] selection = Selection.GetFiltered (typeof(Object), SelectionMode.DeepAssets);
		List <Texture2D> sprites = new List<Texture2D> ();

		foreach (Object obj in selection) {
			if (obj is Texture2D) {
				Texture2D tex = obj as Texture2D;
				sprites.Add (tex);
			}
		}


		PackTexture (sprites, 512, 512, 1);
	}

	private static void PackTexture(List <Texture2D> sprites, int width, int height, int padding) {
		if (width > 2048 || height > 2048) {
			return;
		}
		List <SpriteMetaData> spriteSheets = new List<SpriteMetaData> ();
		List <Rect> spaces = new List<Rect> ();
		Dictionary<string, List<Texture2D>> textureCache = new Dictionary<string, List<Texture2D>> ();
		Dictionary<Texture2D, Rect> rectCache = new Dictionary<Texture2D, Rect> ();

		Texture2D resultTexture = new Texture2D (width, height, TextureFormat.RGBA32, false, true);
		sprites.Sort (delegate(Texture2D x, Texture2D y) {
			return - (x.width * x.height).CompareTo(y.width * y.height);	
		});
		spaces.Add (new Rect (0, 0, width, height));



		foreach (Texture2D sp in sprites) {
			
//			if (GetRectFromCache(sp, rectCache, ref frame)) {
//				SpriteMetaData metaData = new SpriteMetaData();
//				metaData.name = sp.name;
//				metaData.rect = frame;
//				metaData.pivot = Vector2.zero;
//				spriteSheets.Add (metaData);
//				continue;
//			}
			bool deploy = false;
			Rect frame = default(Rect);
			Rect usageRect = default(Rect);
			for (int i = 0; i < spaces.Count; i++) {
				Rect space = spaces [i];
				Vector2 usageSize = new Vector2 (sp.width + padding, sp.height + padding);
				if (space.width >= usageSize.x && space.height >= usageSize.y) {
					usageRect = new Rect (space.x, space.y, usageSize.x, usageSize.y);

					frame = new Rect (space.x, space.y, sp.width, sp.height);
					Color[] colors = sp.GetPixels ();
					resultTexture.SetPixels ((int)frame.x, (int)frame.y, (int)frame.width, (int)frame.height, colors);
					deploy = true;

					SpriteMetaData metaData = new SpriteMetaData();
					metaData.name = sp.name;
					metaData.rect = frame;
					metaData.pivot = Vector2.zero;
					spriteSheets.Add (metaData);
					break;
				}
//				if (i > 10) {
//					Debug.LogError("stack overflow!!!");
//					break;
//				}
			}
			if (deploy) {
				SplitSpaces (spaces, usageRect);
			}

			if (!deploy) {
				Debug.LogError ("pack failed!" + width + "x" + height);
				Debug.LogError ("spaces:" + spaces.Count);
				PackTexture (sprites, width > height ? width : width * 2, width > height ? height * 2 : height, padding);

				return;
			}
		}


		byte[] data = resultTexture.EncodeToPNG ();
		string filePath = Application.dataPath + "/Atlas/output.png";
		File.WriteAllBytes (filePath, data);
		AssetDatabase.ImportAsset (Application.dataPath + "/Atlas/");
		AssetDatabase.Refresh ();

		TextureImporter impt = AssetImporter.GetAtPath ("Assets/Atlas/output.png") as TextureImporter;
		impt.spritesheet = spriteSheets.ToArray();
		impt.mipmapEnabled = false;
		impt.alphaIsTransparency = false;
		impt.filterMode = FilterMode.Bilinear;
		impt.textureType = TextureImporterType.Advanced;
		impt.textureFormat = TextureImporterFormat.RGBA32;
		impt.SaveAndReimport ();
	}
	private static bool GetRectFromCache(Texture2D src, Dictionary<Texture2D, Rect> rectCache, ref Rect rect) {
		foreach (Texture2D keyTexture in rectCache.Keys) {
			if (src.width == keyTexture.width && src.height == keyTexture.height) {
				if (CompateTexture (src, keyTexture)) {
					rect = rectCache[keyTexture];
					return true;
				}
			}
		}
		return false;
	}

	private static bool CompateTexture(Texture2D src, Texture2D dst) {
		if (src.width != dst.width || src.height != dst.height) {
			return false;
		}
		Color[] cSrc = src.GetPixels ();
		Color[] cDst = src.GetPixels ();
		for (int i = 0; i < cSrc.Length; i++) {
			if (cSrc [i] != cDst [i]) {
				return false;
			}
		}
		return true;
	}

	private static List<Rect> SplitSpace(Rect space, Vector2 usageSize) {
		List<Rect> results = new List<Rect> ();
		if (space.width > usageSize.x) {
			Rect r = new Rect (space.x + usageSize.x, space.y, space.width - usageSize.x, usageSize.y);
			results.Add (r);
		}
		if (space.height > usageSize.y) {
			Rect r = new Rect (space.x, space.y + usageSize.y, space.width, space.height - usageSize.y);
			results.Add (r);
		}
		return results;
	}

	private static void SplitSpaces(List<Rect> spaces, Rect usageRect) {
//		Debug.Log (spaces.Count);
		int times = 0;
		for (int i = 0; i < spaces.Count; i++) {
			Rect space = spaces [i];
			times++;
//			if (times > 10) {
//				Debug.LogError("stack overflow!!!" + i);
//				break;
//			}

			if (space.Overlaps (usageRect)) {
//				Debug.Log (space + "   " + usageRect);
				spaces.RemoveAt (i);
				if (usageRect.x > space.x) {
					Rect r = new Rect (space.x, space.y, usageRect.x - space.x, space.height);
//					Debug.Log ("add 1 " + r);
					if (r.width > 0 && r.height > 0) spaces.Insert (i++, r);
				}

				if (usageRect.x + usageRect.width < space.x + space.width ) {
					Rect r = new Rect (usageRect.x + usageRect.width, space.y, space.x + space.width - usageRect.x - usageRect.width, space.height);
//					Debug.Log ("add 2 " + r);
					if (r.width > 0 && r.height > 0) spaces.Insert (i++, r);
				} 

				if (usageRect.y > space.y) {
					Rect r = new Rect (space.x, space.y, space.width, usageRect.y - space.y);
//					Debug.Log ("add 3 "  + r);
					if (r.width > 0 && r.height > 0) spaces.Insert (i++, r);
				}

				if (usageRect.y + usageRect.height < space.y + space.height) {
					Rect r = new Rect (space.x, usageRect.y + usageRect.height, space.width, space.y + space.height - usageRect.y - usageRect.height);
//					Debug.Log ("add 4 "  + r);
					if (r.width > 0 && r.height > 0) spaces.Insert (i++, r);
				}

				i--;
//				Debug.Log ("count:" + spaces.Count + " " +  i);
			}
		}
	}

}
