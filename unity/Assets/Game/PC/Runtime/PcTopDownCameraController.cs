#nullable enable

namespace PampaSkylines.PC
{
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class PcTopDownCameraController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 18f;
    [SerializeField] private float dragPanSpeed = 0.03f;
    [SerializeField] private float edgeScrollSpeed = 10f;
    [SerializeField] private float edgeScrollMargin = 18f;
    [SerializeField] private float zoomSpeed = 4f;
    [SerializeField] private float rotationSpeed = 70f;
    [SerializeField] private float minZoom = 4f;
    [SerializeField] private float maxZoom = 40f;
    [SerializeField] private float movementSmoothTime = 0.08f;

    private Camera? _camera;
    private Vector3 _lastMousePosition;
    private Vector3 _positionVelocity;
    private bool _edgeScrollEnabled = true;

    public void SetEdgeScrollEnabled(bool enabled)
    {
        _edgeScrollEnabled = enabled;
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize <= 0f ? 12f : _camera.orthographicSize, minZoom, maxZoom);
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = 500f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.90f, 0.93f, 0.96f);

        if (transform.position == Vector3.zero)
        {
            transform.SetPositionAndRotation(new Vector3(12f, 18f, -12f), Quaternion.Euler(55f, 45f, 0f));
        }
    }

    private void Update()
    {
        if (_camera is null)
        {
            return;
        }

        var flattenedForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        var flattenedRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (_edgeScrollEnabled)
        {
            input += ReadEdgeScrollInput();
        }

        var movementScale = moveSpeed * Mathf.Max(0.5f, _camera.orthographicSize / 12f);
        var targetPosition = transform.position + ((flattenedForward * input.y) + (flattenedRight * input.x)) * (movementScale * Time.unscaledDeltaTime);

        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.up, -rotationSpeed * Time.unscaledDeltaTime, Space.World);
        }

        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.unscaledDeltaTime, Space.World);
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) > 0.001f)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize - (scrollDelta * zoomSpeed), minZoom, maxZoom);
        }

        if (Input.GetMouseButtonDown(2))
        {
            _lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(2))
        {
            var delta = Input.mousePosition - _lastMousePosition;
            targetPosition -= (flattenedRight * delta.x + flattenedForward * delta.y) * (dragPanSpeed * Mathf.Max(0.5f, _camera.orthographicSize / 12f));
            _lastMousePosition = Input.mousePosition;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _positionVelocity, movementSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    private Vector2 ReadEdgeScrollInput()
    {
        var input = Vector2.zero;
        var mouse = Input.mousePosition;

        if (mouse.x <= edgeScrollMargin)
        {
            input.x -= edgeScrollSpeed / moveSpeed;
        }
        else if (mouse.x >= Screen.width - edgeScrollMargin)
        {
            input.x += edgeScrollSpeed / moveSpeed;
        }

        if (mouse.y <= edgeScrollMargin)
        {
            input.y -= edgeScrollSpeed / moveSpeed;
        }
        else if (mouse.y >= Screen.height - edgeScrollMargin)
        {
            input.y += edgeScrollSpeed / moveSpeed;
        }

        return input;
    }
}
}
