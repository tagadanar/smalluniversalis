﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WorldMapStrategyKit {
	[Serializable]
	[ExecuteInEditMode]
	[RequireComponent(typeof(WMSK))]
	public class WMSK_Ticker : MonoBehaviour {

		/// <summary>
		/// This is a constant but can be increased if needed - should be an even number (zero will be on the equator)
		/// </summary>
		[NonSerialized]
		public const int
			NUM_TICKERS = 9;

		/// <summary>
		/// The ticker bands where ticker texts scrolls.
		/// </summary>
		[SerializeField]
		public TickerBand[]
			tickerBands;
		// collection of ticker bands. Changes in its fields have effect immediately on the map.

		[SerializeField]
		bool _overlayMode;

		/// <summary>
		/// When enabled, tickers will be drawn in front of Camera
		/// </summary>
		public bool overlayMode {
			get { return _overlayMode; }
			set {
				if (value != _overlayMode) {
					_overlayMode = value;
					if (tickerBaseLayer != null) {
						DestroyImmediate(tickerBaseLayer);
						UpdateTickerBands();
					}
				}
			}
		}


		/// <summary>
		/// Accesor to the World Map Strategt Kit API
		/// </summary>
		public WMSK map { get { return GetComponent<WMSK>(); } }

		#region internal variables

		GameObject tickerBaseLayer;
		Font defaultFont;
		Material defaultShadowMaterial;

		#endregion

		#region Lifecycle events

		void Awake() {
			Init();
		}

		void OnDestroy() {
			GameObject overlayLayer = map.GetOverlayLayer(false);
			if (overlayLayer == null)
				return;
			Transform t = overlayLayer.transform.Find("TickersLayer");
			if (t != null)
				DestroyImmediate(t.gameObject);
		}

		void Update() {
			UpdateTickerBands();
		}

		public void Init() {
			if (tickerBands == null || tickerBands.Length != NUM_TICKERS) {
				tickerBands = new TickerBand[NUM_TICKERS];
				for (int k = 0; k < NUM_TICKERS; k++) {
					tickerBands[k] = new TickerBand();
					ResetTickerBand(k);
				}
			}

			if (defaultFont == null) {
				defaultFont = Instantiate(Resources.Load <Font>("WMSK/Font/Lato"));
				defaultFont.name = "Lato";
				defaultFont.hideFlags = HideFlags.DontSave;
				Material fontMaterial = Instantiate(Resources.Load<Material>("WMSK/Materials/Font")); // this material is linked to a shader that has into account zbuffer
				fontMaterial.mainTexture = defaultFont.material.mainTexture;
				//fontMaterial.hideFlags = HideFlags.DontSave;
				defaultFont.material = fontMaterial;
				fontMaterial.renderQueue += 5;
			}
			if (defaultShadowMaterial == null) {
				defaultShadowMaterial = Instantiate(defaultFont.material);
				//defaultShadowMaterial.hideFlags = HideFlags.DontSave;
				defaultShadowMaterial.renderQueue--;
			}

			UpdateTickerBands();
		}

		public bool UpdateTickerBands() {
			bool changes = false;
			
			if (tickerBands == null)
				return changes;
			
			// Check base ticker layer
			if (tickerBaseLayer == null) {
				changes = true;
				Camera cameraMain = map.cameraMain;
				GameObject overlayLayer = overlayMode ? cameraMain.gameObject : map.GetOverlayLayer(true);
				if (overlayLayer == null)
					return changes;
				Transform t = overlayLayer.transform.Find("TickersLayer");
				if (t == null) {
					tickerBaseLayer = new GameObject("TickersLayer");
					tickerBaseLayer.hideFlags = HideFlags.DontSave;
					tickerBaseLayer.transform.SetParent(overlayLayer.transform, false);
				} else {
					tickerBaseLayer = t.gameObject;
				}
				if (_overlayMode) {
					float z = cameraMain.nearClipPlane + 0.005f;
					tickerBaseLayer.transform.localPosition = new Vector3(0, 0, z);
					Vector3 pos0 = cameraMain.transform.InverseTransformPoint(cameraMain.ScreenToWorldPoint(new Vector3(0, 0, z)));
					Vector3 pos1 = cameraMain.transform.InverseTransformPoint(cameraMain.ScreenToWorldPoint(new Vector3(cameraMain.pixelWidth, cameraMain.pixelHeight, z)));
					tickerBaseLayer.transform.localScale = new Vector3(pos1.x - pos0.x, pos1.y - pos0.y, 1);
				} else {
					tickerBaseLayer.transform.localPosition = new Vector3(0, 0, -0.03f); 
				}
				tickerBaseLayer.layer = overlayLayer.layer;
			}

			// Redraw tickers
			for (int k = 0; k < tickerBands.Length; k++) {
				TickerBand tickerBand = tickerBands[k];
				// Check ticker layer
				GameObject tickerBandLayer = tickerBands[k].gameObject;
				if (tickerBandLayer == null) {
					changes = true;
					// Check if it's in the scene
					string name = "Ticker" + k.ToString();
					Transform t = tickerBaseLayer.transform.Find(name);
					if (t != null) {
						tickerBandLayer = t.gameObject;
					} else {
						tickerBandLayer = Instantiate(Resources.Load<GameObject>("WMSK/Prefabs/TickerBand"));
						tickerBandLayer.name = name;
						tickerBandLayer.hideFlags = HideFlags.DontSave;
						tickerBandLayer.transform.SetParent(tickerBaseLayer.transform, false);
						tickerBandLayer.layer = tickerBaseLayer.layer;
						Material mat = Instantiate(tickerBandLayer.GetComponent<Renderer>().sharedMaterial);
						mat.hideFlags = HideFlags.DontSave;
						tickerBandLayer.GetComponent<Renderer>().sharedMaterial = mat;
						mat.color = new Color(0, 0, 0, 0);
					}
					tickerBand.gameObject = tickerBandLayer;
				}

				// Controls visibility
				float goodAlpha;
				if (tickerBand.visible && !(tickerBand.autoHide && GetTickerTextCount(k) == 0)) {
					goodAlpha = tickerBand.backgroundColor.a;
				} else {
					goodAlpha = 0;
				}
				float currentAlpha = tickerBandLayer.GetComponent<Renderer>().sharedMaterial.color.a;
				if (!tickerBand.alphaChanging && currentAlpha != goodAlpha) {
					tickerBand.alphaChanging = true;
					tickerBand.alphaStart = currentAlpha;
					tickerBand.alphaTimer = DateTime.Now;
				}
				if (!tickerBandLayer.activeSelf && tickerBand.alphaChanging)
					tickerBandLayer.SetActive(true);
				// Assign customizable properties
				if (tickerBandLayer.activeSelf) {
					// position
					float posY = tickerBand.verticalOffset;
					if (tickerBandLayer.transform.localPosition.y != posY) {
						tickerBandLayer.transform.localPosition = new Vector3(0, posY, tickerBandLayer.transform.localPosition.z);
						changes = true;
					}
					// vertical scale
					float scaleY = tickerBand.verticalSize; // * WMSK.mapHeight;
					if (tickerBandLayer.transform.localScale.y != scaleY) {
						tickerBandLayer.transform.localScale = new Vector3(1.0f, scaleY, 1.0f);
						changes = true;
					}
					// alpha
					float alpha;
					if (tickerBand.alphaChanging) {
						DateTime now = DateTime.Now;
						float elapsed = (float)(now - tickerBand.alphaTimer).TotalSeconds;
						alpha = Mathf.Lerp(tickerBand.alphaStart, goodAlpha, elapsed / tickerBand.fadeSpeed);
						if (alpha == goodAlpha) {
							tickerBand.alphaChanging = false;
							if (goodAlpha == 0)
								tickerBandLayer.SetActive(false);
						}
					} else {
						alpha = goodAlpha;
					}
					// Assigns the color & alpha
					Color color = new Color(tickerBand.backgroundColor.r, tickerBand.backgroundColor.g, tickerBand.backgroundColor.b, alpha);
					Material mat = tickerBandLayer.GetComponent<Renderer>().sharedMaterial;
					if (mat.color != color) {
						mat.color = color;
						changes = true;
					}
				}
			}

			return changes;
		}

		#endregion


		#region Public API

		/// <summary>
		/// Returns number of active ticker texts for all ticker bands.
		/// </summary>
		public int GetTickerTextCount() {
			if (tickerBaseLayer != null)
				return tickerBaseLayer.GetComponentsInChildren<TickerTextAnimator>(true).Length;
			else
				return 0;
		}

		/// <summary>
		/// Returns number of active ticker texts in specified ticker band
		/// </summary>
		public int GetTickerTextCount(int tickerLine) {
			GameObject tickerBand = tickerBands[tickerLine].gameObject;
			if (tickerBand != null)
				return tickerBand.GetComponentsInChildren<TickerTextAnimator>(true).Length;
			else
				return 0;
		}

		/// <summary>
		/// Returns the number of active ticker bands
		/// </summary>
		public int GetTickerBandsActiveCount() {
			int c = 0;
			for (int k = 0; k < tickerBands.Length; k++) {
				if (tickerBands[k].gameObject != null && tickerBands[k].gameObject.activeSelf)
					c++;
			}
			return c;
		}


		/// <summary>
		/// Set default parameters for all ticker bands and removes any text on them.
		/// </summary>
		public void ResetTickerBands() {
			for (int k = 0; k < tickerBands.Length; k++)
				ResetTickerBand(k);
		}

		/// <summary>
		/// Resets ticker band properties to its defaults and removes any ticker text on it.
		/// </summary>
		/// <param name="tickerIndex">Ticker index.</param>
		public void ResetTickerBand(int tickerBandIndex) {
			if (tickerBands == null)
				Init();
			if (tickerBandIndex < 0 || tickerBandIndex >= tickerBands.Length) {
				Debug.LogWarning("Ticker band index out of range.");
				return;
			}
			TickerBand tickerBand = tickerBands[tickerBandIndex];
			tickerBand.verticalSize = 1.0f / NUM_TICKERS;
			float vpos = (1.0f / NUM_TICKERS) * tickerBandIndex;
			if (vpos > 0.5f)
				vpos -= 1.0f;
			tickerBand.verticalOffset = vpos;
			tickerBand.autoHide = false;
			tickerBand.backgroundColor = new Color(0, 0, 0, 0.9f);  // default color, can be changed
			GameObject layer = tickerBand.gameObject;
			if (layer != null) {
				TickerTextAnimator[] tta = tickerBand.gameObject.GetComponentsInChildren<TickerTextAnimator>(true);
				for (int k = 0; k < tta.Length; k++) {
					DestroyImmediate(tta[k].gameObject);
				}
			}
		}

		/// <summary>
		/// Adds a new ticker text to a ticker line.
		/// </summary>
		public void AddTickerText(TickerText tickerText) {
			if (tickerText == null) {
				Debug.LogWarning("Tried to add an empty ticker. Ignoring.");
				return;
			}
			if (tickerBands == null) {
				Debug.LogWarning("Tickers has not been initialized properly.");
				return;
			}
			if (tickerText.tickerLine < 0 || tickerText.tickerLine >= tickerBands.Length) {
				Debug.LogWarning("Ticker line " + tickerText.tickerLine + " doesn't exist.");
				return;
			}
			TickerBand tickerBand = tickerBands[tickerText.tickerLine];
			GameObject tickerBandObj = tickerBand.gameObject;
			if (tickerBandObj == null) {
				Debug.LogWarning("Ticker band " + tickerText.tickerLine + " has been destroyed. Can't add text.");
				return;
			}

			Font customFont = tickerText.font ?? defaultFont;
			Material customShadowMaterial = tickerText.shadowMaterial ?? defaultShadowMaterial;

			// Creates TextMesh Object
			Vector2 pos = new Vector2(tickerText.horizontalOffset, 0);
			TickerTextAnimator[] previous = tickerBandObj.GetComponentsInChildren<TickerTextAnimator>();
			GameObject tickerTextObj = Drawing.CreateText(tickerText.text, null, pos, customFont, tickerText.textColor, tickerText.drawTextShadow, customShadowMaterial, tickerText.shadowColor, tickerText.anchor).gameObject;
			tickerTextObj.layer = tickerBandObj.layer;
			if (tickerText.drawTextShadow) {
				tickerTextObj.transform.Find("shadow").gameObject.layer = tickerTextObj.layer;
			}
			tickerText.gameObject = tickerTextObj;
			tickerText.textMeshSize = tickerTextObj.GetComponent<Renderer>().bounds.size;
			tickerTextObj.transform.SetParent(tickerBandObj.transform, false);

			// Apply scale (text size)
			Vector3 parentSize = new Vector3(WMSK.mapWidth, tickerBand.verticalSize * WMSK.mapHeight, 1.0f);

			float textScale = 0.003f * tickerText.textMeshSize.y * tickerText.textScale;
			tickerTextObj.transform.localScale = new Vector3(textScale * parentSize.y / parentSize.x, textScale, 1.0f);
			tickerText.textMeshSize = new Vector2(WMSK.mapWidth * tickerBandObj.transform.localScale.x * tickerText.textMeshSize.x * tickerTextObj.transform.localScale.x, 
				WMSK.mapHeight * tickerBandObj.transform.localScale.y * tickerText.textMeshSize.y * tickerTextObj.transform.localScale.y);
			tickerTextObj.AddComponent<TickerTextAnimator>().tickerText = tickerText;

			// Position the text
			float x = pos.x;
			if (tickerBand.scrollSpeed != 0) {
				x = 0.5f + 0.5f * tickerText.textMeshSize.x / parentSize.x;
				// Adds other previous tickertexts on the band
				float widthSum = 0;
				for (int p = 0; p < previous.Length; p++) {
					widthSum += previous[p].tickerText.textMeshSize.x / parentSize.x;
				}
				if (widthSum > 0)
					x = x + 0.01f + widthSum;
				if (tickerBand.scrollSpeed > 0)
					x = -x;
			}
			pos = new Vector3(x, tickerTextObj.transform.localPosition.y);
			tickerTextObj.transform.localPosition = new Vector3(pos.x, 0.06f, _overlayMode ? -0.0001f : -0.002f);
		}

		#endregion



	}



}
