using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BioEden.NoDOF
{
    public sealed class ModUpdates : MonoBehaviour
    {
        private const string Releases = "https://github.com/h0n24/bioeden-visual-accessibility/releases";
        private static ModUpdates instance;
        public static string InstalledVersion => typeof(ModUpdates).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "Unknown";
        private Text status;
        private Button check;
        private UnityWebRequest request;
        private float nextCheck;


        public static void Open()
        {
            if (instance != null) { instance.gameObject.SetActive(true); return; }
            var root = new GameObject("BioEden mod updates", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            instance = root.AddComponent<ModUpdates>();
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(700, 290);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.14f, 0.18f, 1f);
            instance.status = MakeText(panel.transform, "BioEden Visual Accessibility\nInstalled: " + InstalledVersion + "\nCheck GitHub for updates (includes beta releases).", new Vector2(0, 55), new Vector2(650, 150));
            instance.check = MakeButton(panel.transform, "Check GitHub", -215, () => instance.StartCoroutine(instance.Check()));
            MakeButton(panel.transform, "Open downloads", 0, () => Application.OpenURL(Releases));
            MakeButton(panel.transform, "Close", 215, () => Destroy(root));
        }

        private IEnumerator Check()
        {
            if (request != null || Time.realtimeSinceStartup < nextCheck) yield break;
            nextCheck = Time.realtimeSinceStartup + 30f;
            check.interactable = false;
            status.text = "Installed: " + InstalledVersion + "\nChecking GitHub…";
            request = UnityWebRequest.Get("https://api.github.com/repos/h0n24/bioeden-visual-accessibility/releases?per_page=100");
            request.timeout = 10;
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "BioEden-Visual-Accessibility/" + InstalledVersion);
            yield return request.SendWebRequest();
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException("Connection failed: HTTP " + request.responseCode + " — " + request.error);
                string tag = ReleaseVersion.NewestDownload(request.downloadHandler.text);
                if (!ReleaseVersion.TryVersion(tag, out Version newest)) throw new InvalidOperationException("GitHub returned no supported downloadable release.");
                if (!ReleaseVersion.TryVersion(InstalledVersion, out Version installed)) throw new InvalidOperationException("Installed version could not be read.");
                status.text = "Installed: " + InstalledVersion + "\n" + (newest > installed ? "Update available: " + tag : "No newer downloadable release found.") +
                    "\nTo update: close BioEden, extract the latest ZIP\nand run Install.cmd. No uninstall is needed.";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BioEden.NoDOF] Update check: HTTP " + request.responseCode + "; " + e);
                string reason = request.result != UnityWebRequest.Result.Success
                    ? (request.responseCode == 403 || request.responseCode == 429 ? "GitHub refused the request (HTTP " + request.responseCode + ")." : "Connection failed: " + request.error)
                    : "Could not read GitHub's release information.";
                status.text = "Installed: " + InstalledVersion + "\n" + reason + "\nUse Open downloads. Details are in Player.log.";
            }
            finally { request.Dispose(); request = null; }
            while (Time.realtimeSinceStartup < nextCheck) yield return null;
            check.interactable = true;
        }

        private static Text MakeText(Transform parent, string value, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = pos; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.text = value; text.raycastTarget = false;
            return text;
        }
        private static Button MakeButton(Transform parent, string caption, float x, UnityEngine.Events.UnityAction click)
        {
            var go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(x, -95); rect.sizeDelta = new Vector2(195, 46);
            go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.5f, 1f);
            MakeText(go.transform, caption, Vector2.zero, rect.sizeDelta);
            var button = go.GetComponent<Button>(); button.onClick.AddListener(click); return button;
        }
        private void OnDestroy() { if (request != null) { request.Abort(); request.Dispose(); request = null; } if (instance == this) instance = null; }
    }
}

