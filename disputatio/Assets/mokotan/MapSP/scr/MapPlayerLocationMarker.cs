using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapPlayerLocationMarker : MonoBehaviour
{
    [SerializeField] private RectTransform floor1Marker;
    [SerializeField] private RectTransform floor2Marker;
    [SerializeField] private GameObject floor1Object;
    [SerializeField] private GameObject floor2Object;
    [SerializeField] private bool activateCurrentFloor = true;
    [SerializeField] private ControlFloor controlFloor;

    private string lastSceneName;

    private struct MarkerLocation
    {
        public MarkerLocation(int floor, Vector2 position)
        {
            Floor = floor;
            Position = position;
        }

        public int Floor { get; }
        public Vector2 Position { get; }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(lastSceneName, sceneName, StringComparison.Ordinal))
            return;

        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();

        string sceneName = SceneManager.GetActiveScene().name;
        lastSceneName = sceneName;

        if (!TryGetLocation(sceneName, out MarkerLocation location))
        {
            SetMarkerVisible(floor1Marker, false);
            SetMarkerVisible(floor2Marker, false);
            return;
        }

        RectTransform activeMarker = location.Floor == 2 ? floor2Marker : floor1Marker;
        RectTransform inactiveMarker = location.Floor == 2 ? floor1Marker : floor2Marker;

        SetMarkerVisible(inactiveMarker, false);
        SetMarkerVisible(activeMarker, true);
        if (activeMarker != null)
        {
            activeMarker.anchoredPosition = location.Position;
            activeMarker.SetAsLastSibling();
        }

        if (activateCurrentFloor)
        {
            ActivateFloor(location.Floor);
        }
    }

    private void ResolveReferences()
    {
        if (controlFloor == null)
            controlFloor = GetComponentInChildren<ControlFloor>(true);
        if (controlFloor == null)
            controlFloor = GetComponentInParent<ControlFloor>(true);
        if (floor1Marker == null)
            floor1Marker = FindChildRect("PlayerLocationMarker_1F");
        if (floor2Marker == null)
            floor2Marker = FindChildRect("PlayerLocationMarker_2F");
        if (floor1Object == null)
            floor1Object = FindChildObject("1Floor");
        if (floor2Object == null)
            floor2Object = FindChildObject("2Floor");
    }

    private RectTransform FindChildRect(string childName)
    {
        GameObject child = FindChildObject(childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private GameObject FindChildObject(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                return child.gameObject;
        }

        return null;
    }

    private void ActivateFloor(int floor)
    {
        if (controlFloor != null)
        {
            if (floor == 2)
                controlFloor.ActivateFloor2();
            else
                controlFloor.ActivateFloor1();
            return;
        }

        if (floor1Object != null) floor1Object.SetActive(floor == 1);
        if (floor2Object != null) floor2Object.SetActive(floor == 2);
    }

    private static void SetMarkerVisible(RectTransform marker, bool visible)
    {
        if (marker != null)
            marker.gameObject.SetActive(visible);
    }

    public static bool TryGetLocationForScene(string sceneName, out int floor, out Vector2 position)
    {
        if (TryGetLocation(sceneName, out MarkerLocation location))
        {
            floor = location.Floor;
            position = location.Position;
            return true;
        }

        floor = 0;
        position = Vector2.zero;
        return false;
    }

    private static bool TryGetLocation(string sceneName, out MarkerLocation location)
    {
        switch (sceneName)
        {
            case SceneNames.MainScene:
            case "Hall_playerble":
            case "Hall_animate":
                location = new MarkerLocation(1, Vector2.zero);
                return true;

            case "Hall_Left":
            case "Hall_Left2":
            case "Hallway_Left":
            case "Hallway_Left2":
                location = new MarkerLocation(1, new Vector2(-250f, 0f));
                return true;

            case SceneNames.Kitchen:
            case "UtilityRoom":
                location = new MarkerLocation(1, new Vector2(-321f, 293.42548f));
                return true;

            case SceneNames.HallRight:
            case "Hall_Right2":
            case "Hall_RightCross":
            case "Hallway_Right":
            case "Hallway_Right2":
                location = new MarkerLocation(1, new Vector2(250f, 0f));
                return true;

            case SceneNames.StudyRoom:
            case "StudyRoomCutScene":
                location = new MarkerLocation(1, new Vector2(339f, -129f));
                return true;

            case SceneNames.MaidRoom:
                location = new MarkerLocation(1, new Vector2(347f, 342f));
                return true;

            case "2floorMainHall":
                location = new MarkerLocation(2, new Vector2(0f, -420f));
                return true;

            case "2floorLeft":
                location = new MarkerLocation(2, new Vector2(-288f, -250f));
                return true;

            case "2floorLeftCross":
                location = new MarkerLocation(2, new Vector2(-288f, 105f));
                return true;

            case "2floorHallway_Left":
                location = new MarkerLocation(2, new Vector2(-288f, 105f));
                return true;

            case "2floorRight":
                location = new MarkerLocation(2, new Vector2(349f, -250f));
                return true;

            case "2floorRightCross":
                location = new MarkerLocation(2, new Vector2(349f, 106f));
                return true;

            case "2floorHallway_Right":
                location = new MarkerLocation(2, new Vector2(349f, 106f));
                return true;

            case "ChildEntrance":
            case SceneNames.ChildRoom:
                location = new MarkerLocation(2, new Vector2(-256f, 342f));
                return true;

            case "TutorEntrance":
            case SceneNames.TutorRoom:
                location = new MarkerLocation(2, new Vector2(-321f, -108f));
                return true;

            case "WifeEntrance":
            case "DressingRoom":
            case SceneNames.WifeRoom:
                location = new MarkerLocation(2, new Vector2(339f, -129f));
                return true;

            case "BedEntrance":
            case SceneNames.BedRoom:
                location = new MarkerLocation(2, new Vector2(339f, 348f));
                return true;

            default:
                location = default(MarkerLocation);
                return false;
        }
    }
}
