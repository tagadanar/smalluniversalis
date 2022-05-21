// World Map Strategy Kit for Unity - Main Script
// (C) 2016-2021 by Ramiro Oliva (Kronnect)
// Don't modify this script - changes could be lost if you upgrade to a more recent version of WMSK

using UnityEngine;
using System;
using WorldMapStrategyKit.Poly2Tri;


namespace WorldMapStrategyKit {

	public partial class WMSK : MonoBehaviour {

		#region Region related functions

		struct BorderIntersection {
			public Vector2 p;
			public float dist;
		}

		Material extrudedMat;
		BorderIntersection[] intersections;

		bool ComputeCurvedLabelData(Region region) {

			if (!region.curvedLabelInfo.isDirty) {
				return true;
            }
			region.curvedLabelInfo.isDirty = false;

			Vector2[] points = region.points;
			if (points == null)
				return false;

			// Step 1: compute maximum axis distance
			int iStart = 0, iEnd = points.Length / 2;
			float maxDist = float.MinValue;

			for (int k = 0; k < points.Length; k += 2) {
				for (int j = k + 1; j < points.Length; j += 2) {
					float dx = points[k].x - points[j].x;
					dx *= _countryLabelsHorizontality;
					float dy = points[k].y - points[j].y;
					float dist = dx * dx + dy * dy;
					if (dist > maxDist) {
						maxDist = dist;
						iStart = k;
						iEnd = j;
					}
				}
			}

			if (points[iStart].x > points[iEnd].x) {
				int u = iEnd;
				iEnd = iStart;
				iStart = u;
			}

			// Step 2: compute intersections between segment which is perpendicular to max axis to find a good centroid inside the polygon
			Vector2 axisStart = points[iStart];
			Vector2 axisEnd = points[iEnd];
			Vector2 axis = axisEnd - axisStart;
			Vector2 axisMid = (axisStart + axisEnd) * 0.5f;
			Vector2 dir = new Vector2(-axis.y, axis.x).normalized;
			Vector2 s0 = axisMid - dir;
			Vector2 s1 = axisMid + dir;
			if (intersections == null || intersections.Length < 10) {
				intersections = new BorderIntersection[10];
			}

			// Compute all intersections
			int intersectionIndex = -1;
			for (int k = 0; k < points.Length; k++) {
				Vector2 intersectionPoint;
				int next = k < points.Length - 1 ? k + 1 : 0;
				if (TestSegmentIntersection(s0, s1, points[k], points[next], out intersectionPoint)) {
					intersectionIndex++;
					if (intersectionIndex >= intersections.Length)
						break;
					float dx = intersectionPoint.x - s0.x;
					float dy = intersectionPoint.y - s0.y;
					float dist = dx * dx + dy * dy;
					intersections[intersectionIndex].p = intersectionPoint;
					intersections[intersectionIndex].dist = dist;
				}
			}

			Vector2 p0 = axisStart, p1 = axisEnd;
			if ((intersectionIndex % 2) == 0) {
				intersectionIndex--;
			}
												
			// Sort intersections by distance
			for (int k = 0; k < intersectionIndex; k++) {
				bool changes = false;
				for (int j = k + 1; j <= intersectionIndex; j++) {
					if (intersections[j].dist < intersections[k].dist) {
						Vector2 oldp = intersections[k].p;
						float oldDist = intersections[k].dist;
						intersections[k].p = intersections[j].p;
						intersections[k].dist = intersections[j].dist;
						intersections[j].p = oldp;
						intersections[j].dist = oldDist;
						changes = true;
					}
				}
				if (!changes)
					break;
			}

			// Iterate intersections in pairs and get the thicker one
			maxDist = float.MinValue;
			for (int k = 0; k < intersectionIndex; k += 2) {
				float diff = intersections[k + 1].dist - intersections[k].dist;
				if (diff > maxDist) {
					maxDist = diff;
					p0 = intersections[k].p;
					p1 = intersections[k + 1].p;
				}
			}

			// Land intersection points
			region.curvedLabelInfo.p0 = p0;
			region.curvedLabelInfo.p1 = p1;

			// Corrected centroid
			Vector2 centroid = (p0 + p1) * 0.5f;
			region.curvedLabelInfo.axisAveragedThickness = p1 - p0;

			// Reduce axis length
			region.curvedLabelInfo.axisStart = centroid + (axisStart - centroid) * _countryLabelsLength;
			region.curvedLabelInfo.axisEnd = centroid + (axisEnd - centroid) * _countryLabelsLength;

			// Final axis and displacement values
			axisMid = (axisStart + axisEnd) * 0.5f;
			region.curvedLabelInfo.axisMidDisplacement = centroid - axisMid;
			axis = axisEnd - axisStart;
			// note the multiplication of axis.x by 2 to compensate map aspect ratio
			region.curvedLabelInfo.axisAngle = Mathf.Atan2(axis.y, axis.x * 2f) * Mathf.Rad2Deg;

			return true;
		}

		// Find the point of intersection between
		// the lines p1 --> p2 and p3 --> p4.
		bool TestSegmentIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersectionPoint) {
			float dx12 = p2.x - p1.x;
			float dy12 = p2.y - p1.y;
			float dx34 = p4.x - p3.x;
			float dy34 = p4.y - p3.y;
			intersectionPoint.x = 0;
			intersectionPoint.y = 0;

			// Solve for t1 and t2
			float denominator = dy12 * dx34 - dx12 * dy34;
			if (denominator == 0)
				return false;
			float t1 =	((p1.x - p3.x) * dy34 + (p3.y - p1.y) * dx34) / denominator;
			if (t1 < 0 || t1 > 1)
				return false;

			float t2 = ((p3.x - p1.x) * dy12 + (p1.y - p3.y) * dx12) / -denominator;

			if (t2 >= 0 && t2 <= 1) {
				intersectionPoint.x = p1.x + dx12 * t1;
				intersectionPoint.y = p1.y + dy12 * t1;
				return true;
			} else {
				return false;
			}
		}


		Vector2 ConvertToTextureCoordinates(Vector3 vertex, int width, int height) {
			Vector2 v;
			v.x = (int)((vertex.x + 0.5f) * width);
			v.y = (int)((vertex.y + 0.5f) * height);
			return v;
		}

		/// <summary>
		/// Paints the region into a given texture color array.
		/// </summary>
		/// <param name="region">Region.</param>
		/// <param name="color">Color.</param>
		public void RegionPaint(Color[] colors, int textureWidth, int textureHeight, Region region, Color color) {

			// Get the region mesh
			int entityIndex, regionIndex;
			bool isCountry = region.entity is Country;
			bool hideSurface = false;
			GameObject surf;

			if (isCountry) {
				entityIndex = GetCountryIndex((Country)region.entity);
				regionIndex = GetCountryRegionIndex(entityIndex, region);
				surf = GetCountryRegionSurfaceGameObject(entityIndex, regionIndex);
				if (surf == null) {
					surf = ToggleCountryRegionSurface(entityIndex, regionIndex, true, Color.white);
					hideSurface = true;
				}
			} else {
				entityIndex = GetProvinceIndex((Province)region.entity);
				regionIndex = GetProvinceRegionIndex(entityIndex, region);
				surf = GetCountryRegionSurfaceGameObject(entityIndex, regionIndex);
				if (surf == null) {
					surf = ToggleProvinceRegionSurface(entityIndex, regionIndex, true, Color.white);
					hideSurface = true;
				}
			}
			if (surf == null)
				return;

			// Get triangles and paint over the texture
			MeshFilter mf = surf.GetComponent<MeshFilter>();
			if (mf == null || mf.sharedMesh.GetTopology(0) != MeshTopology.Triangles)
				return;
			Vector3[] vertices = mf.sharedMesh.vertices;
			int[] indices = mf.sharedMesh.GetTriangles(0);

			float maxEdge = textureWidth * 0.8f;
			float minEdge = textureWidth * 0.2f;
			for (int i = 0; i < indices.Length; i += 3) {
				Vector2 p1 = ConvertToTextureCoordinates(vertices[indices[i]], textureWidth, textureHeight);
				Vector2 p2 = ConvertToTextureCoordinates(vertices[indices[i + 1]], textureWidth, textureHeight);
				Vector2 p3 = ConvertToTextureCoordinates(vertices[indices[i + 2]], textureWidth, textureHeight);
				// Sort points
				if (p2.x > p3.x) {
					Vector2 p = p2;
					p2 = p3;
					p3 = p;
				}
				if (p1.x > p2.x) {
					Vector2 p = p1;
					p1 = p2;
					p2 = p;
					if (p2.x > p3.x) {
						p = p2;
						p2 = p3;
						p3 = p;
					}
				}
				if (p1.x < minEdge && p2.x < minEdge && p3.x > maxEdge) {
					if (p1.x < 1 && p2.x < 1) {
						p1.x = textureWidth - p1.x;
						p2.x = textureWidth - p2.x;
					} else
						p3.x = textureWidth - p3.x;
				} else if (p1.x < minEdge && p2.x > maxEdge && p3.x > maxEdge) {
					p1.x = textureWidth + p1.x;
				} 
				Drawing.DrawTriangle(colors, textureWidth, textureHeight, p1, p2, p3, color);
			}

			if (hideSurface) {
				if (isCountry) {
					ToggleCountryRegionSurface(entityIndex, regionIndex, false, Color.white);
				} else {
					ToggleProvinceRegionSurface(entityIndex, regionIndex, false, Color.white);
				}
			}
		}


		/// <summary>
		/// Paints the region into a given texture color array.
		/// </summary>
		/// <param name="region">Region.</param>
		/// <param name="color">Color.</param>
		public void RegionPaintHeights(Color[] colors, int textureWidth, int textureHeight, Region region, float[]heights, float minHeight, int heightsWidth, int heightsHeight, Gradient gradient ) {

			// Get the region mesh
			int entityIndex, regionIndex;
			bool isCountry = region.entity is Country;
			bool hideSurface = false;
			GameObject surf;

			if (isCountry) {
				entityIndex = GetCountryIndex((Country)region.entity);
				regionIndex = GetCountryRegionIndex(entityIndex, region);
				surf = GetCountryRegionSurfaceGameObject(entityIndex, regionIndex);
				if (surf == null) {
					surf = ToggleCountryRegionSurface(entityIndex, regionIndex, true, Color.white);
					hideSurface = true;
				}
			} else {
				entityIndex = GetProvinceIndex((Province)region.entity);
				regionIndex = GetProvinceRegionIndex(entityIndex, region);
				surf = GetCountryRegionSurfaceGameObject(entityIndex, regionIndex);
				if (surf == null) {
					surf = ToggleProvinceRegionSurface(entityIndex, regionIndex, true, Color.white);
					hideSurface = true;
				}
			}
			if (surf == null)
				return;

			// Get triangles and paint over the texture
			MeshFilter mf = surf.GetComponent<MeshFilter>();
			if (mf == null || mf.sharedMesh.GetTopology(0) != MeshTopology.Triangles)
				return;
			Vector3[] vertices = mf.sharedMesh.vertices;
			int[] indices = mf.sharedMesh.GetTriangles(0);

			float maxEdge = textureWidth * 0.8f;
			float minEdge = textureWidth * 0.2f;
			for (int i = 0; i < indices.Length; i += 3) {
				Vector2 p1 = ConvertToTextureCoordinates(vertices[indices[i]], textureWidth, textureHeight);
				Vector2 p2 = ConvertToTextureCoordinates(vertices[indices[i + 1]], textureWidth, textureHeight);
				Vector2 p3 = ConvertToTextureCoordinates(vertices[indices[i + 2]], textureWidth, textureHeight);
				// Sort points
				if (p2.x > p3.x) {
					Vector2 p = p2;
					p2 = p3;
					p3 = p;
				}
				if (p1.x > p2.x) {
					Vector2 p = p1;
					p1 = p2;
					p2 = p;
					if (p2.x > p3.x) {
						p = p2;
						p2 = p3;
						p3 = p;
					}
				}
				if (p1.x < minEdge && p2.x < minEdge && p3.x > maxEdge) {
					if (p1.x < 1 && p2.x < 1) {
						p1.x = textureWidth - p1.x;
						p2.x = textureWidth - p2.x;
					} else
						p3.x = textureWidth - p3.x;
				} else if (p1.x < minEdge && p2.x > maxEdge && p3.x > maxEdge) {
					p1.x = textureWidth + p1.x;
				} 
				Drawing.DrawTriangle(colors, textureWidth, textureHeight, p1, p2, p3, heights, minHeight, heightsWidth, heightsHeight, gradient);
			}

			if (hideSurface) {
				if (isCountry) {
					ToggleCountryRegionSurface(entityIndex, regionIndex, false, Color.white);
				} else {
					ToggleProvinceRegionSurface(entityIndex, regionIndex, false, Color.white);
				}
			}
		}



		/// <summary>
		/// Creates an extruded version of a given region
		/// </summary>
		/// <returns>The generate extrude game object.</returns>
		/// <param name="name">Name.</param>
		/// <param name="extrusionAmount">Size of the extrusion.</param>
		/// <param name="region">Region.</param>
		/// <param name="material">Material.</param>
		/// <param name="textureScale">Texture scale.</param>
		/// <param name="textureOffset">Texture offset.</param>
		/// <param name="textureRotation">Texture rotation.</param>
		public GameObject RegionGenerateExtrudeGameObject(string name, Region region, float extrusionAmount, Color sideColor) {
			Material sideMaterial = Instantiate<Material>(extrudedMat);
			sideMaterial.color = sideColor;
			Material topMaterial = Instantiate<Material>(extrudedMat);
			topMaterial.mainTexture = earthMat.mainTexture;
			return RegionGenerateExtrudeGameObject(name, region, extrusionAmount, topMaterial, sideMaterial, Misc.Vector2one, Misc.Vector2zero, 0, false);
		}



		/// <summary>
		/// Creates an extruded version of a given region
		/// </summary>
		/// <returns>The generate extrude game object.</returns>
		/// <param name="name">Name.</param>
		/// <param name="extrusionAmount">Size of the extrusion.</param>
		/// <param name="region">Region.</param>
		/// <param name="material">Material.</param>
		/// <param name="textureScale">Texture scale.</param>
		/// <param name="textureOffset">Texture offset.</param>
		/// <param name="textureRotation">Texture rotation.</param>
		public GameObject RegionGenerateExtrudeGameObject(string name, Region region, float extrusionAmount, Material material, Material sideMaterial) {
			return RegionGenerateExtrudeGameObject(name, region, extrusionAmount, material, sideMaterial, Misc.Vector2one, Misc.Vector2zero, 0);
		}

		/// <summary>
		/// Creates an extruded version of a given region
		/// </summary>
		/// <returns>The generate extrude game object.</returns>
		/// <param name="name">Name.</param>
		/// <param name="extrusionAmount">Size of the extrusion.</param>
		/// <param name="region">Region.</param>
		/// <param name="material">Material.</param>
		/// <param name="textureScale">Texture scale.</param>
		/// <param name="textureOffset">Texture offset.</param>
		/// <param name="textureRotation">Texture rotation.</param>
		public GameObject RegionGenerateExtrudeGameObject(string name, Region region, float extrusionAmount, Material topMaterial, Material sideMaterial, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool useRegionRect = true) {
			if (region == null || region.points.Length < 3) {
				return null;
			}

			Rect rect = useRegionRect ? region.rect2D : new Rect(0.5f, 0.5f, 1f, 1f);
			GameObject go = new GameObject(name);
			go.transform.SetParent(transform, false);

			Poly2Tri.Polygon poly = new Poly2Tri.Polygon(region.points);
			P2T.Triangulate(poly);

			// Creates surface mesh
			GameObject surf = Drawing.CreateSurface("RegionTop", poly, topMaterial, rect, textureScale, textureOffset, textureRotation, null);	
			surf.transform.SetParent(go.transform, false);
			surf.transform.localPosition = new Vector3(0, 0, -extrusionAmount);

			// Create side band
			int pointCount = region.points.Length;
			Vector3[] vertices = new Vector3[pointCount * 2];
			int[] indices = new int[pointCount * 6];
			int vi = 0, ii = -1;
			for (int k = 0; k < pointCount; k++,vi += 2) {
				vertices[vi] = region.points[k];
				vertices[vi].z = -extrusionAmount;
				vertices[vi + 1] = vertices[vi];
				vertices[vi + 1].z = 0;
				if (k == pointCount - 1) {
					indices[++ii] = vi + 1;
					indices[++ii] = vi;
					indices[++ii] = 1;
					indices[++ii] = vi + 1;
					indices[++ii] = 1;
					indices[++ii] = 0;
				} else {
					indices[++ii] = vi;
					indices[++ii] = vi + 1;
					indices[++ii] = vi + 2;
					indices[++ii] = vi + 1;
					indices[++ii] = vi + 3;
					indices[++ii] = vi + 2;
				}
			}

			GameObject band = new GameObject("RegionBand");
			band.transform.SetParent(go.transform, false);
			Mesh mesh = new Mesh();
			mesh.vertices = vertices;
			mesh.triangles = indices;
			mesh.RecalculateNormals();
			MeshFilter mf = band.AddComponent<MeshFilter>();
			mf.mesh = mesh;
			MeshRenderer mr = band.AddComponent<MeshRenderer>();
			mr.sharedMaterial = sideMaterial;

			if (region.entity.allowHighlight && (region.entity is Country && _enableCountryHighlight || region.entity is Province && _enableProvinceHighlight)) {
				ExtrudedRegionInteraction interaction = go.AddComponent<ExtrudedRegionInteraction>();
				interaction.map = this;
				interaction.region = region;
				interaction.topMaterial = topMaterial;
				interaction.sideMaterial = sideMaterial;
				interaction.highlightColor = region.entity is Country ? _fillColor : _provincesFillColor;
			}
			return go;
		}


		GameObject DrawRegionOutlineMesh (string name, Region region, bool overridesAnimationSpeed = false, float animationSpeed = 0f)
		{
			int[] indices = new int[region.points.Length + 1];
			for (int k = 0; k < indices.Length; k++) {
				indices [k] = k;
			}
			indices [indices.Length - 1] = 0; 
			GameObject boldFrontiers = new GameObject (name);

			bool customBorder = region.customBorderTexture != null || region.customBorderTintColor != Color.white;

			if (_outlineDetail == OUTLINE_DETAIL.Simple && !customBorder) {
				Mesh mesh = new Mesh ();
				Vector3[] points = new Vector3[region.points.Length];
				for (int k = 0; k < region.points.Length; k++)
					points [k] = region.points [k];
				mesh.vertices = points; 
				mesh.SetIndices (indices, MeshTopology.LineStrip, 0);
				mesh.RecalculateBounds ();
				if (disposalManager!=null) disposalManager.MarkForDisposal (mesh);

				MeshFilter mf = boldFrontiers.AddComponent<MeshFilter> ();
				mf.sharedMesh = mesh;

				MeshRenderer mr = boldFrontiers.AddComponent<MeshRenderer> ();
				mr.receiveShadows = false;
				mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
				mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				mr.sharedMaterial = outlineMatSimple;
			} else {
				LineRenderer lr = boldFrontiers.AddComponent<LineRenderer> ();
				lr.useWorldSpace = false;
				if (customBorder) {
					lr.startWidth = region.customBorderWidth;
					lr.endWidth = region.customBorderWidth;
				} else {
					lr.startWidth = _outlineWidth;
					lr.endWidth = _outlineWidth;
				}
				int pCount = region.points.Length;
				lr.positionCount = pCount + 1;
				lr.textureMode = LineTextureMode.Tile;
				for (int k = 0; k < pCount; k++) {
					lr.SetPosition (k, region.points [k]);
				}
				lr.SetPosition (pCount, region.points [0]);
				lr.loop = true;
				if (customBorder && region.customBorderTexture != outlineMatTextured.mainTexture) {
					Material mat = Instantiate (outlineMatTextured);
					if (disposalManager!=null) disposalManager.MarkForDisposal (mat);
					mat.name = outlineMatTextured.name;
					mat.mainTexture = region.customBorderTexture;
					mat.mainTextureScale = new Vector2 (region.customBorderTextureTiling, 1f);
					mat.color = region.customBorderTintColor;
					if (!overridesAnimationSpeed) {
						animationSpeed = region.customBorderAnimationSpeed;
					}
					mat.SetFloat ("_AnimationAcumOffset", region.customBorderAnimationAcumOffset);
					region.customBorderAnimationStartTime = time;
					mat.SetFloat ("_AnimationStartTime", time);
					mat.SetFloat ("_AnimationSpeed", animationSpeed);
					lr.sharedMaterial = mat;
				} else {
					lr.sharedMaterial = outlineMatTextured;
				}
			}
			return boldFrontiers;
		}


		bool CheckScreenAreaSizeOfRegion (Region region) {
			Camera cam = currentCamera;
			Vector2 scrTR = cam.WorldToViewportPoint (transform.TransformPoint (region.rect2D.max));
			Vector2 scrBL = cam.WorldToViewportPoint (transform.TransformPoint (region.rect2D.min));
			Rect scrRect = new Rect (scrBL.x, scrTR.y, Math.Abs (scrTR.x - scrBL.x), Mathf.Abs (scrTR.y - scrBL.y));
			float highlightedArea = Mathf.Clamp01 (scrRect.width * scrRect.height);
			return highlightedArea < _highlightMaxScreenAreaSize;
		}

		#endregion

	}

}