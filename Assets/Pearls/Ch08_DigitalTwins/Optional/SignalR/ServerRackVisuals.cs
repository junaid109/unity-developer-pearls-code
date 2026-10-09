using UnityEngine;
using Pearls.DigitalTwins;

public class ServerRackVisuals : MonoBehaviour
{
    public string TwinID = "Rack_001";
    public Renderer StatusLight;

    // Handed in by whatever owns the connection (a bootstrap script, a DI
    // container such as VContainer, or a plain serialized reference).
    private SignalRTwinService _service;

    public void Bind(SignalRTwinService service)
    {
        _service = service;
        _service.OnPropertyUpdated += HandleUpdate;
    }

    void OnDestroy()
    {
        if (_service != null) _service.OnPropertyUpdated -= HandleUpdate;
    }

    // Runs on the main thread: SignalRTwinService only raises this event from
    // FlushPendingUpdates(), which a MonoBehaviour ticks from Update(). The
    // view therefore needs no dispatcher and can touch Unity objects directly.
    private void HandleUpdate(TwinState state)
    {
        if (state.TwinId != TwinID || state.PropertyName != "status") return;

        StatusLight.material.color =
            state.Value == "Error" ? Color.red : Color.green;
    }
}