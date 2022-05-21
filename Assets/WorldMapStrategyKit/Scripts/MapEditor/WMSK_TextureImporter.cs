#if !UNITY_WSA
using UnityEngine;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace WorldMapStrategyKit {
	
	public enum TerritoriesImporterMode {
		Countries = 0,
		Provinces = 1
	}

	public class WMSK_TextureImporter {

		const int MAX_THREADS = 16;
		const int LAST_FILTER = 4;
		const int REPLACEABLE_COLOR = 37;
		const string BACKGROUND_COUNTRY = "";

		enum DIR {
			TR,
			R,
			BR,
			B,
			BL,
			L,
			TL,
			T
		}

		struct ThreadSlot {
			public Thread thread;
			public bool busy;
			public int y0, y1;
			public int colorIndex;
			public List<Vector2[]> points;
			public Vector2[] positions;
			public Color32[] colors;
		}

		public float progress;
		public string results;
		public Texture2D texture;
		public TerritoriesImporterMode mode;
		public bool snapToCountryFrontiers;

		float[] directionXOffset = new float[] { -0.15f, 0.00f, 0.15f, 0.25f, -0.15f,  0.00f, -0.15f, -0.25f };
		float[] directionYOffset = new float[] { 0.15f, 0.25f, 0.15f, 0.00f,  0.15f, -0.25f, -0.15f,  0.00f };

		int[] directionDX = new int[] { 1, 1, 1, 0, -1, -1, -1, 0 };
		int[] directionDY = new int[] { 1, 0, -1, -1, -1, 0, 1, 1 };


		public int goodColorCount {
			get {
				if (goodColors != null)
					return goodColors.Count;
				else
					return 0;
			}
		}

		int y, tw, th;
		Color32[] colors;
		Color32[] colorsAux;
		public List<Color32> goodColors;
		Dictionary<Color32, int> goodColorsDict;
		int colorIndex;
		static Color32 color0 = new Color32 (0, 0, 0, 0);
		Color32 backgroundColor;
		int filter, totalPasses;
		public string status;
		public List<IAdminEntity> entities;
		int actualReplaceableColors, totalReplaceableColors;
		ThreadSlot[] threads;


		public WMSK_TextureImporter (Texture2D texture, Color32 backgroundColor, int detail) {
			progress = 0f;
			results = "";
			tw = texture.width;
			th = texture.height;
			colors = texture.GetPixels32 ();
			colorsAux = new Color32[colors.Length];
			this.texture = texture;
			this.backgroundColor = backgroundColor;
			entities = new List<IAdminEntity> ();
			threads = new ThreadSlot[MAX_THREADS];
			for (int k = 0; k < threads.Length; k++) {
				threads [k].points = new List<Vector2[]> ();
				threads [k].positions = new Vector2[16000];
			}

			GetTerritoriesColors (detail);
			if (goodColors.Count == 0) {
				return;
			}
			colorIndex = 0;
			filter = -1;
			NextFilter ();

		}


		public bool IsGoodColor (Color32 color) {
			return goodColorsDict.ContainsKey (color);
		}

		public void AddGoodColor (Color32 color) {
			if (!goodColorsDict.ContainsKey (color)) {
				int count = goodColors.Count;
				goodColors.Add (color);
				goodColorsDict [color] = count;
			}
		}

		public void ClearGoodColors () {
			goodColors.Clear ();
			goodColorsDict.Clear ();
		}

		public Country[] GetCountries () {
			if (mode != TerritoriesImporterMode.Countries)
				return null;
			int entitiesCount = entities.Count;
			for (int k = 0; k < entitiesCount; k++)
				if (entities [k].regions.Count == 0) {
					entities.RemoveAt (k);
					entitiesCount--;
					if (k > 0)
						k--;
				}
			entitiesCount = entities.Count;

			// Check for holes
			for (int c = 0; c < entitiesCount; c++) {
				Country country = (Country)entities [c];
				if (country.name.Equals (BACKGROUND_COUNTRY))
					continue;
				WMSK.instance.RefreshCountryGeometry (country);
				for (int r0 = 0; r0 < country.regions.Count; r0++) {
					Region region0 = country.regions [r0];
					for (int r1 = 0; r1 < country.regions.Count; r1++) {
						if (r1 == r0)
							continue;
						Region region1 = country.regions [r1];
						if (region0.Contains (region1)) {
							// region1 is a hole
							if (!CountryBordersTooNearRegion (country, region1)) {
								Country bgCountry = GetBackgroundCountry ();
								bgCountry.regions.Add (region1);
							}
							country.regions.Remove (region1);
							r0 = -1;
							break;
						}
					}
				}
			}

			entitiesCount = entities.Count;
			Country[] newCountries = new Country[entitiesCount];
			for (int k = 0; k < entitiesCount; k++)
				newCountries [k] = (Country)entities [k];
			return newCountries;
		}


		/// <summary>
		/// Ensures that a region is not too near a country borders
		/// Returns true if they're too near
		/// </summary>
		bool CountryBordersTooNearRegion (Country country, Region region) {
			const float threshold = 0.0001f;
			int crc = country.regions.Count;
			for (int k = 0; k < crc; k++) {
				Region cRegion = country.regions [k];
				if (cRegion == region)
					return true;
				Vector2[] points0 = region.points;
				Vector2[] points1 = cRegion.points;
				for (int p0 = 0; p0 < points0.Length; p0++) {
					float p0x = points0 [p0].x;
					float p0y = points0 [p0].y;
					for (int p1 = 0; p1 < points1.Length; p1++) {
						float p1x = points1 [p1].x;
						float p1y = points1 [p1].y;
						float dx = p0x - p1x;
						float dy = p0y - p1y;
						dx = dx < 0 ? -dx : dx;
						dy = dy < 0 ? -dy : dy;
						if (dx < threshold || dy < threshold) {
							return true;
						}
					}
				}
			}
			return false;
		}

		Country GetBackgroundCountry () {
			for (int k = 0; k < entities.Count; k++) {
				Country country = (Country)entities [k];
				if (country.name.Equals (BACKGROUND_COUNTRY)) {
					return country;
				}
			}
			Country bgCountry = new Country (BACKGROUND_COUNTRY, "World", 0);
			bgCountry.hidden = true;
			entities.Add (bgCountry);
			return bgCountry;
		}

		public Province[] GetProvinces () {
			if (mode != TerritoriesImporterMode.Provinces)
				return null;
			int entitiesCount = entities.Count;
			for (int k = 0; k < entitiesCount; k++) {
				if (entities [k].regions == null || entities [k].regions.Count == 0) {
					entities.RemoveAt (k);
					entitiesCount--;
					k--;
				}
			}
			entitiesCount = entities.Count;
			Province[] newProvinces = new Province[entitiesCount];
			for (int k = 0; k < entitiesCount; k++) {
				newProvinces [k] = (Province)entities [k];
			}
			return newProvinces;
		}

		void GetTerritoriesColors (int detail) {
			Color32 prevColor = color0;
			int colorCount = colors.Length;
			if (goodColors == null) {
				goodColors = new List<Color32> (256);
				goodColorsDict = new Dictionary<Color32, int> (256);
			} else {
				goodColors.Clear ();
				goodColorsDict.Clear ();
			}

			totalReplaceableColors = 0;
			actualReplaceableColors = 0;
			for (int j = 2; j < th - 2; j++) {
				int jj = j * tw;
				for (int k = 2; k < tw - 2; k++) {
					int index = jj + k;
					Color32 co = colors [index];
					if (isSameRGB (co, prevColor))
						continue;
					if (!isGoodColor (co))
						continue;
					bool same = true;
					// Check neighbours
					for (int j0 = j - detail; j0 <= j + detail && same; j0++) {
						int j0j0 = j0 * tw;
						for (int k0 = k - detail; k0 <= k + detail; k0++) {
							int otherIndex = j0j0 + k0;
							Color32 otherColor = colors [otherIndex];
							if (!isSameRGB (co, otherColor)) {
								same = false;
								break;
							}
						}
					}
					if (same) {
						int count = goodColors.Count;
						goodColorsDict [co] = count;
						goodColors.Add (co);
						prevColor = co;
					} else {
						// Mark this color as replaceable
						colors [index].a = REPLACEABLE_COLOR;
						totalReplaceableColors++;
					}
				}
			}
			actualReplaceableColors = totalReplaceableColors;
																								
		}

		public void CancelOperation () {
			IssueTextureUpdate ();
			progress = 1f;
		}

		public void IssueTextureUpdate () {
			if (texture != null) {
				if (filter == 1) {
					texture.SetPixels32 (colorsAux);
				} else {
					texture.SetPixels32 (colors);
				}
				texture.Apply ();
			}
		}

		bool isGoodColor (Color32 co) {
			return co.a > 0 && !isSameRGB (co, backgroundColor) && !goodColorsDict.ContainsKey (co);
		}

		public void StartProcess (TerritoriesImporterMode mode, bool snapToCountryFrontiers) {
			this.mode = mode;
			this.snapToCountryFrontiers = snapToCountryFrontiers;
			this.totalPasses = 5;
			if (snapToCountryFrontiers && mode == TerritoriesImporterMode.Provinces)
				totalPasses++;
			entities = new List<IAdminEntity> (goodColors.Count);
			switch (mode) {
			case TerritoriesImporterMode.Countries: 
				for (int k = 0; k < goodColors.Count; k++) {
					entities.Add (new Country ("Country " + k.ToString (), "World", k));
				}
				break;
			case TerritoriesImporterMode.Provinces: 
				for (int k = 0; k < goodColors.Count; k++) {
					entities.Add (new Province ("Province " + k.ToString (), -1, k));
				}
				break;
			}
			filter = 0;
			progress = 0f;
		}

		public void Process () {
			results = "";
			switch (filter) {
			case 0: 
				FilterRemoveBackground ();
				break;
			case 1: 
				FilterFindEdges ();
				break;
			case 2: 
				FilterExtractRegions ();
				break;
			case 3:
				FilterSnapRegions ();
				break;
			case 4:
				FilterPolishJoints ();
				break;
			case 5:
				FilterSnapToCountryFrontiers ();
				break;
			}
			if (progress >= 1f)
				NextFilter ();
		}

		void NextFilter () {
			filter++;
			switch (filter) {
			case 0:
				status = "Removing background and odd colors (pass 1/" + totalPasses + ")...";
				break;
			case 1:
				status = "Finding edges (pass 2/" + totalPasses + ")...";
				break;
			case 2:
				colorIndex = 0;
				status = "Extracting regions (pass 3/" + totalPasses + ")...";
				break;
			case 3:
				snapEntityIndex = 0;
				status = "Snapping regions (pass 4/" + totalPasses + ")...";
				break;
			case 4:
				polishEntityIndex = 0;
				status = "Polishing joints (pass 5/" + totalPasses + ")...";
				break;
			case 5:
				if (mode == TerritoriesImporterMode.Provinces && snapToCountryFrontiers) {
					snapToCountryFrontiersIndex = 0;
					status = "Snapping to country frontiers (pass 6/" + totalPasses + ")...";
				} else {
					filter++;
				}
				break;
			}

			texture.SetPixels32 (colors);
			texture.Apply ();

			if (filter < totalPasses - 1)
				progress = 0;

		}

		void FilterRemoveBackground () {
			// Check odd colors
			if (progress < 0.9f) {
				bool changes = false;
				int max = colors.Length - tw;
				for (int k = 0; k < max; k++) {
					Color32 co = colors [k];
					if (co.a == REPLACEABLE_COLOR) {
						Color32 bottom = colors [k + tw];
						if (bottom.a != REPLACEABLE_COLOR) {
							colors [k] = bottom;
							changes = true;
							actualReplaceableColors--;
						} else {
							Color32 right = colors [k + 1];
							if (right.a != REPLACEABLE_COLOR) {
								colors [k] = right;
								changes = true;
								actualReplaceableColors--;
							}
						}
					}
				}
				if (!changes)
					progress = 0.9f;
				else
					progress = 0.9f * (1f - (float)actualReplaceableColors / totalReplaceableColors);
			} else {
				for (int k = 0; k < colors.Length; k++) {
					Color32 co = colors [k];
					if (isSameRGB (co, backgroundColor)) {
						colors [k] = color0;
					}
				}
				progress = 1f;
			}
		}

		void FilterFindEdges () {
			// Check thread status
			int activeThreads = 0;
			for (int t = 0; t < threads.Length; t++) {
				if (threads [t].busy) {
					activeThreads++;
					// Manage current threads
					if (!threads [t].thread.IsAlive) {
						progress += (threads [t].y1 - threads [t].y0 + 1f) / th;
						threads [t].busy = false;
					}
				} else {
					// Check if there's pending work
					if (y < th - 1) {
						threads [t].busy = true;
						int slice = 24;
						int y1 = y + slice;
						if (y1 > th)
							y1 = th - 1;
						threads [t].y0 = y;
						y = y1;
						threads [t].y1 = y1;
						int thisSlice = t;
						threads [t].thread = new Thread (() => {
							Thread.CurrentThread.IsBackground = true; 
							FindEdgesInSlice (thisSlice);
						});
						threads [t].thread.Start ();
						activeThreads++;
					}
				}
			}
			// Ensure progress is not 1 until all threads have finished
			if (progress >= 1f)
				progress = 0.99f;
			if (activeThreads == 0) {
				progress = 1f;
				Array.Copy (colorsAux, colors, colors.Length);
			}
		}

		void FindEdgesInSlice (int t) {
			for (int y = threads [t].y0; y <= threads [t].y1; y++) {
				int pos = y * tw;
				for (int x = 0; x < tw; x++,pos++) {
					Color32 c = colors [pos];
					if (y > 0 && x > 0 && y < th - 1 && x < tw - 1) {
						Color32 n = colors [pos - tw];
						Color32 w = colors [pos - 1];
						Color32 e = colors [pos + 1];
						Color32 s = colors [pos + tw];
						if (c.r == n.r && c.r == w.r && c.r == e.r && c.r == s.r &&
						    c.g == n.g && c.g == w.g && c.g == e.g && c.g == s.g &&
						    c.b == n.b && c.b == w.b && c.b == e.b && c.b == s.b) {
							c = color0;
						}
					}
					colorsAux [pos] = c;
				}
			}
		}

		void FilterExtractRegions () {

			// Check thread status
			int activeThreads = 0;
			for (int t = 0; t < threads.Length; t++) {
				if (threads [t].busy) {
					activeThreads++;
					// Manage current threads
					if (!threads [t].thread.IsAlive) {
						int listCount = threads [t].points.Count;
						for (int k = 0; k < listCount; k++) {
							Vector2[] points = threads [t].points [k];
							switch (mode) {
							case TerritoriesImporterMode.Countries:
								AddNewCountryRegion (points, threads [t].colorIndex);
								break;
							case TerritoriesImporterMode.Provinces:
								AddNewProvinceRegion (points, threads [t].colorIndex);
								break;
							}
						}
						progress += 1f / goodColorCount;
						if (progress >= 1f)
							progress = 0.99f;
						threads [t].busy = false;
					}
				} else {
					// Check if there's pending work
					if (colorIndex < goodColorCount) {
						threads [t].busy = true;
						threads [t].colorIndex = colorIndex++;
						threads [t].points.Clear ();
						if (threads [t].colors == null) {
							threads [t].colors = new Color32[colors.Length];
						}
						Array.Copy (colors, threads [t].colors, colors.Length);
						int thisThread = t;
						threads [t].thread = new Thread (() => {
							Thread.CurrentThread.IsBackground = true; 
							FilterExtractRegions (thisThread);
						});
						threads [t].thread.Start ();
						activeThreads++;
					}
				}
			}
			if (activeThreads == 0) {
				progress = 1f;
			}

		}


		void FilterExtractRegions (int t) {
			int colorIndex = threads [t].colorIndex;
			Color32 currentColor = goodColors [colorIndex];
			Color32[] colors = threads [t].colors;
			int colorArrayIndex = colors.Length - 1;

			// Locate one point for current color
			while (colorArrayIndex >= 0) {
				if (colors [colorArrayIndex].a > 0 && isSameRGB (colors [colorArrayIndex], currentColor)) {
					Vector2[] points = GetRegionForCurrentColor (t, colorArrayIndex, currentColor);
					if (points.Length > 5) {
						threads [t].points.Add (points);
					}
				}
				colorArrayIndex--;
			}
		}



		//		void FilterExtractRegions () {
		//			if (colorIndex >= goodColorCount || progress >= 1f) {
		//				progress = 1f;
		//				return;
		//			}
		//			currentColor = goodColors [colorIndex];
		//			progress = (float)colorIndex / goodColorCount;
		//
		//			// Locate one point for current color
		//			while (colorArrayIndex >= 0) {
		//				if (colors [colorArrayIndex].a > 0 && isSameRGB (colors [colorArrayIndex], currentColor))
		//					break;
		//				colorArrayIndex--;
		//			}
		//			if (colorArrayIndex < 0) {
		//				colorArrayIndex = colors.Length - 1;
		//				colorIndex++;
		//				return;
		//			}
		//
		//			Vector2[] points = GetRegionForCurrentColor ();
		//			if (points.Length > 5) {
		//				switch (mode) {
		//				case TerritoriesImporterMode.Countries:
		//					AddNewCountryRegion (points);
		//					break;
		//				case TerritoriesImporterMode.Provinces:
		//					AddNewProvinceRegion (points);
		//					break;
		//				}
		//			}
		//		}

		void AddNewCountryRegion (Vector2[] points, int colorIndex) {
			Country country = (Country)entities [colorIndex];
			Region region = new Region (country, country.regions.Count);
			region.points = points;
			country.regions.Add (region);
		}

		void AddNewProvinceRegion (Vector2[] points, int colorIndex) {
			// Compute province center
			float minX = float.MaxValue;
			float maxX = float.MinValue;
			float minY = float.MaxValue;
			float maxY = float.MinValue;
			int posCount = points.Length;
			for (int k = 0; k < posCount; k++) {
				if (points [k].x < minX)
					minX = points [k].x;
				if (points [k].x > maxX)
					maxX = points [k].x;
				if (points [k].y < minY)
					minY = points [k].y;
				if (points [k].y > maxY)
					maxY = points [k].y;
			}
			Vector2 provinceCenter = new Vector2 ((maxX + minX) * 0.5f, (maxY + minY) * 0.5f);
				
			Province province = (Province)entities [colorIndex];
			if (province.countryIndex < 0) {
				// Get country at province center
				int countryIndex = WMSK.instance.GetCountryIndex (provinceCenter);
				if (countryIndex < 0)
					return;
				province.countryIndex = countryIndex;
			}
			if (province.regions == null) {
				province.regions = new List<Region> ();
			}
			Region region = new Region (province, province.regions.Count);
			region.points = points;
			province.regions.Add (region);
		}

		Vector2[] GetRegionForCurrentColor (int threadIndex, int colorArrayIndex, Color32 currentColor) {
			Color32[] colors = threads [threadIndex].colors;
			Vector2[] positions = threads [threadIndex].positions;
			int positionsCount = 0;
			int y = colorArrayIndex / tw;
			int x = colorArrayIndex % tw;
			DIR direction = DIR.TR;
			bool hasMoved = true;
			int directionTries = 0;
			Vector2 newPos = Misc.Vector2zero;
			for (int i = 0; i < 200000; i++) {	// safety loop
				if (hasMoved) {
					// Based on direction add a little offset to allow turning back
					newPos.x = x + directionXOffset [(int)direction];
					newPos.y = y + directionYOffset [(int)direction];
					if (positionsCount >= positions.Length) {
						Vector2[] newPositions = new Vector2[positionsCount * 2];
						Array.Copy (positions, newPositions, positions.Length);
						threads [threadIndex].positions = newPositions;
						positions = newPositions;
					}
					positions [positionsCount++] = newPos;
					// has completed a lap?
					if (positionsCount >= 5) {
						float fx = positions [0].x - newPos.x;
						float fy = positions [0].y - newPos.y;
						if (fx * fx + fy * fy < 3f)
							break;
					}
					hasMoved = false;
					directionTries = 0;
					if (i > 0) { 
						direction -= 3;
						if ((int)direction < 0)
							direction = (DIR)(((int)direction + 8) % 8);
					}
				}
				int dx = directionDX [(int)direction];
				int dy = directionDY [(int)direction];
				bool changeDirection = false;
				if (x + dx >= tw - 1 || x + dx <= 0 || y + dy >= th - 1 || y + dy <= 0) {
					changeDirection = true;
				} else {
					Color32 co = colors [(y + dy) * tw + (x + dx)];
					if (co.a > 0 && isSameRGB (co, currentColor)) {
						x += dx;
						y += dy;
						hasMoved = true;
					} else {
						changeDirection = true;
					}
				}
				if (changeDirection) {
					directionTries++;
					if (directionTries > 8)
						break;
					direction++;
					if ((int)direction > 7)
						direction = 0;
				}
			}

			Vector2[] positionsArray = new Vector2[positionsCount];
			for (int k = 0; k < positionsCount; k++) {
				int l = Mathf.RoundToInt (positions [k].y);
				int c = Mathf.RoundToInt (positions [k].x);
				// Extract internal part of region
				colors [l * tw + c] = color0;
				// Convert positions from pixel coordinates to map coordinates
				positionsArray [k].x = (positions [k].x - tw * 0.5f + 0.5f) / tw;
				positionsArray [k].y = (positions [k].y - th * 0.5f + 0.5f) / th;
			}
									
			return positionsArray;
		}


		bool isSameRGB (Color32 co1, Color32 co2) {
			return co1.r == co2.r && co1.g == co2.g && co1.b == co2.b;
		}


		#region Region snapping

		int snapEntityIndex;

		void FilterSnapRegions () {
			if (snapEntityIndex >= entities.Count) {
				progress = 1f;
				return;
			}
			progress = (float)snapEntityIndex / entities.Count;
			IAdminEntity entity = entities [snapEntityIndex];
			if (entity.regions != null) {
				int entityRegionsCount = entity.regions.Count;
				for (int k = 0; k < entityRegionsCount; k++) {
					Region regionTosnap = entities [snapEntityIndex].regions [k];
					SnapRegion (regionTosnap);
				}
			}
			snapEntityIndex++;
		}



		void SnapRegion (Region region) {

			int regionPointsCount = region.points.Length;
			int entitiesCount = entities.Count;

			int m = Mathf.Max (tw, th);
			float threshold = 2f / m;
			threshold *= threshold;

			for (int k = 0; k < regionPointsCount; k++) {
				Vector2 p = region.points [k];
				Vector2 nearPoint = Misc.Vector2zero;
				float minDist = float.MaxValue;
				for (int e = 0; e < entitiesCount; e++) {
					IAdminEntity entity = entities [e];
					if (entity.regions == null)
						continue;
					int entityRegionsCount = entity.regions.Count;
					for (int r = 0; r < entityRegionsCount; r++) {
						Region entityRegion = entity.regions [r];
						if (entityRegion == region) {
							continue;
						}
						int entityRegionsPointsCount = entityRegion.points.Length;
						for (int j = 0; j < entityRegionsPointsCount; j++) {
							Vector2 op = entityRegion.points [j];

							// Check if both points are near
							float d = (p.x - op.x) * (p.x - op.x) + (p.y - op.y) * (p.y - op.y);
							if (d < threshold && d < minDist) {
								nearPoint = op;
								minDist = d;
							}
						}
					}
				}
				// Snap point?
				if (minDist < float.MaxValue) {
					region.points [k] = nearPoint;
				}
			}

			region.RemoveDuplicatePoints ();
		}


		#endregion

		
		#region Polish joins

		int polishEntityIndex;

		void FilterPolishJoints () {
			if (polishEntityIndex >= entities.Count) {
				progress = 1f;
				return;
			}
			progress = (float)polishEntityIndex / entities.Count;
			IAdminEntity entity = entities [polishEntityIndex];
			if (entity.regions != null) {
				int entityRegionsCount = entity.regions.Count;
				for (int k = 0; k < entityRegionsCount; k++) {
					Region region = entities [polishEntityIndex].regions [k];
					PolishRegionJoints (region);
				}
			}
			polishEntityIndex++;
		}

		/// <summary>
		/// For each two points, locate a third point belonging to a different region which is not conected to them and below threshold. Add a new point at that position.
		/// </summary>
		void PolishRegionJoints (Region region) {

			int regionPointsCount = region.points.Length;
			int entitiesCount = entities.Count;
			Vector2 midP = Misc.Vector3zero;

			for (int k = 0; k < regionPointsCount; k++) {
				Vector2 p0 = region.points [k];
				int nextPointIndex = k == regionPointsCount - 1 ? 0 : k + 1;
				Vector2 p1 = region.points [nextPointIndex];
				float p01d = (p0.x - p1.x) * (p0.x - p1.x) + (p0.y - p1.y) * (p0.y - p1.y);
				bool snapped = false;
				for (int e = 0; e < entitiesCount && !snapped; e++) {
					IAdminEntity entity = entities [e];
					if (entity.regions == null)
						continue;
					int entityRegionsCount = entity.regions.Count;
					for (int r = 0; r < entityRegionsCount && !snapped; r++) {
						Region entityRegion = entity.regions [r];
						if (entityRegion == region) {
							continue;
						}
						int entityRegionsPointsCount = entityRegion.points.Length;
						for (int j = 0; j < entityRegionsPointsCount; j++) {
							Vector2 v = entityRegion.points [j];
							float d = (v.x - p0.x) * (v.x - p0.x) + (v.y - p0.y) * (v.y - p0.y);
							if (d > 0 && d < p01d) {
								d = (v.x - p1.x) * (v.x - p1.x) + (v.y - p1.y) * (v.y - p1.y);
								if (d > 0 && d < p01d) {
									// make sure center of triangle is not contained by any region (that would mean the change would create overlap)
									midP.x = (p0.x + p1.x + v.x) / 3f;
									midP.y = (p0.y + p1.y + v.y) / 3f;
									if (!AnyRegionContainsPoint (midP)) {
										// Insert point v in k+1 position
										int rpCount = region.points.Length;
										Vector2[] newPoints = new Vector2[rpCount + 1];
										int np = -1;
										for (int i = 0; i < rpCount; i++) {
											++np;
											if (np == nextPointIndex) {
												newPoints [np] = v;
												np++;
											}
											newPoints [np] = region.points [i];
										}
										region.points = newPoints;
										snapped = true;
										regionPointsCount++;
										break;
									}
								}
							}
						}
					}
				}
			}
		}

		bool AnyRegionContainsPoint (Vector2 p) {
			int entitiesCount = entities.Count;
			for (int k = 0; k < entitiesCount; k++) {
				IAdminEntity entity = entities [k];
				if (entity.regions == null)
					continue;
				int entityRegionsCount = entity.regions.Count;
				for (int r = 0; r < entityRegionsCount; r++) {
					Region region = entity.regions [r];
					if (region.Contains (p))
						return true;
				}
			}
			return false;
		}

		#endregion


		#region Snap to Country Frontiers

		int snapToCountryFrontiersIndex;

		void FilterSnapToCountryFrontiers () {
			if (snapToCountryFrontiersIndex >= entities.Count) {
				progress = 1f;
				return;
			}
			progress = (float)snapToCountryFrontiersIndex / entities.Count;
			IAdminEntity entity = entities [snapToCountryFrontiersIndex];
			if (entity.regions != null) {
				int entityRegionsCount = entity.regions.Count;
				for (int k = 0; k < entityRegionsCount; k++) {
					Region regionTosnap = entities [snapEntityIndex].regions [k];
					SnapToCountryFrontiers (regionTosnap);
				}
			}
			snapToCountryFrontiersIndex++;
		}

		// Snap positions to country frontiers?
		void SnapToCountryFrontiers (Region region) {
			WMSK map = WMSK.instance;
			int positionsCount = region.points.Length;
			for (int k = 0; k < positionsCount; k++) {
				Vector2 p = region.points [k];
				float minDist = float.MaxValue;
				Vector2 nearest = Misc.Vector2zero;
				bool found = false;
				for (int c = 0; c < map.countries.Length; c++) {
					Country country = map.countries [c];
					// if country's bounding box does not contain point, skip it
					if (!country.regionsRect2D.Contains (p))
						continue;
					int regionCount = country.regions.Count;
					for (int cr = 0; cr < regionCount; cr++) {
						Region countryRegion = country.regions [cr];
						// if region's bounding box does not contain point, skip it
						if (!countryRegion.rect2D.Contains (p))
							continue;
						for (int q = 0; q < countryRegion.points.Length; q++) {
							float dist = FastVector.SqrDistance (ref countryRegion.points [q], ref p); // (countryRegion.points [q] - p).sqrMagnitude;
							if (dist < minDist) {
								minDist = dist;
								nearest = region.points [q];
								found = true;
							}
						}
					}
				}
				if (found) {
					region.points [k] = nearest;
				}
			}
			region.RemoveDuplicatePoints ();
		}


		#endregion

	}
}
#endif
