using System.Collections.Generic;
using UnityEngine;

// Brings customers in from the island edge to take free stools.
// Stools can be added or removed in Build Mode.
public class CustomerSpawner : MonoBehaviour
{
    [Tooltip("Customer template (kept disabled; cloned on each arrival)")]
    public GameObject customerTemplate;
    [Tooltip("Other customer types (astronaut, robot...), picked at random along with the template above")]
    public GameObject[] extraTemplates;
    [Tooltip("Ship that brings the customer (empty = customer appears at the edge)")]
    public GameObject shipTemplate;
    [Tooltip("Distance past the edge where the ship parks")]
    public float shipParkOffset = 1.7f;
    [Tooltip("Height at which the parked ship hovers")]
    public float shipParkHeight = -0.35f;
    [Tooltip("Stools where customers wait")]
    public List<Transform> seats = new List<Transform>();
    public float firstDelay = 2f;
    public Vector2 spawnInterval = new Vector2(6f, 12f);
    [Tooltip("Where waiting customers look (Ro's work area)")]
    public Vector3 lookAt = new Vector3(0f, 0f, -1.6f);
    [Tooltip("Island center")]
    public Vector3 islandCenter = Vector3.zero;
    [Tooltip("Distance from the center to the edge where customers arrive and leave")]
    public float edgeRadius = 7.0f;
    [Tooltip("Distance behind the stool where the customer stands")]
    public float standBehind = 0.7f;

    [Tooltip("Paused in Build Mode: nobody new arrives")]
    public bool paused;

    private readonly HashSet<Transform> occupied = new HashSet<Transform>();
    private float nextSpawn;

    // ships on the way: the customer steps off only once the ship parks
    class Arrival { public ShipArrival ship; public Transform seat; }
    private readonly List<Arrival> arrivals = new List<Arrival>();

    void Start()
    {
        nextSpawn = Time.time + firstDelay;
        if (customerTemplate != null) customerTemplate.SetActive(false);
        if (extraTemplates != null) foreach (var t in extraTemplates) if (t != null) t.SetActive(false);
        if (shipTemplate != null) shipTemplate.SetActive(false);
    }

    void Update()
    {
        for (int i = arrivals.Count - 1; i >= 0; i--)
        {
            var a = arrivals[i];
            if (a.ship == null) { FreeSeat(a.seat); arrivals.RemoveAt(i); continue; }
            if (paused || !a.ship.Parked) continue;
            // the stool may have been stored in Build Mode
            if (a.seat == null || !a.seat.gameObject.activeInHierarchy) { a.ship.Depart(); FreeSeat(a.seat); }
            else Spawn(a.seat, a.ship);
            arrivals.RemoveAt(i);
        }

        if (paused) { nextSpawn = Mathf.Max(nextSpawn, Time.time + 2f); return; }
        if (customerTemplate == null || Time.time < nextSpawn) return;
        var seat = RandomFreeSeat();
        if (seat == null) { nextSpawn = Time.time + 1f; return; }
        if (shipTemplate != null) SendShip(seat); else Spawn(seat);
        nextSpawn = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
    }

    // brings a customer right now (if a stool is free); used by the trailer
    public ShipArrival ArriveNow()
    {
        var seat = RandomFreeSeat();
        if (seat == null) return null;
        if (shipTemplate == null) { Spawn(seat); return null; }
        SendShip(seat);
        nextSpawn = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
        return arrivals[arrivals.Count - 1].ship;
    }

    void SendShip(Transform seat)
    {
        occupied.Add(seat);
        GetCustomerPath(seat.position, out _, out Vector3 entry);
        Vector3 outward = entry - islandCenter; outward.y = 0f;
        outward = outward.sqrMagnitude > 0.001f ? outward.normalized : Vector3.back;
        Vector3 park = entry + outward * shipParkOffset + Vector3.up * shipParkHeight;
        var go = Instantiate(shipTemplate, park, Quaternion.identity, transform);
        go.name = "NaveCliente";
        go.SetActive(true);
        var ship = go.GetComponent<ShipArrival>();
        if (ship == null) ship = go.AddComponent<ShipArrival>();
        ship.Init(park, outward);
        arrivals.Add(new Arrival { ship = ship, seat = seat });
    }

    GameObject RandomTemplate()
    {
        int extra = extraTemplates != null ? extraTemplates.Length : 0;
        int i = Random.Range(-1, extra);
        return i < 0 || extraTemplates[i] == null ? customerTemplate : extraTemplates[i];
    }

    Transform RandomFreeSeat()
    {
        var free = new List<Transform>();
        foreach (var s in seats)
            if (s != null && s.gameObject.activeInHierarchy && !occupied.Contains(s)) free.Add(s);
        return free.Count > 0 ? free[Random.Range(0, free.Count)] : null;
    }

    // Where the customer waits (behind the stool) and where they arrive/leave (island edge).
    // A fixed path, so Build Mode can keep that space clear.
    public void GetCustomerPath(Vector3 seatPosition, out Vector3 stand, out Vector3 entry)
    {
        Vector3 seatPos = new Vector3(seatPosition.x, 0f, seatPosition.z);
        Vector3 away = seatPos - new Vector3(lookAt.x, 0f, lookAt.z);
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
        stand = seatPos + away * standBehind;

        Vector3 fromCenter = stand - islandCenter; fromCenter.y = 0f;
        Vector3 dir = fromCenter.sqrMagnitude > 0.01f ? fromCenter.normalized : Vector3.back;
        entry = islandCenter + dir * edgeRadius;
        entry.y = 0f;
    }

    public Customer Spawn(Transform seat, ShipArrival ship = null)
    {
        occupied.Add(seat);
        GetCustomerPath(seat.position, out Vector3 stand, out Vector3 entry);

        var go = Instantiate(RandomTemplate(), entry, Quaternion.LookRotation(stand - entry), transform);
        go.name = "Cliente";
        var c = go.GetComponent<Customer>();
        if (c == null) c = go.AddComponent<Customer>();
        go.SetActive(true);
        c.Init(this, seat, stand, entry, lookAt);
        c.ship = ship;
        return c;
    }

    public void FreeSeat(Transform seat) => occupied.Remove(seat);

    public bool IsSeatOccupied(Transform seat) => seat != null && occupied.Contains(seat);

    public void AddSeat(Transform seat)
    {
        if (seat != null && !seats.Contains(seat)) seats.Add(seat);
    }

    // false if someone is using the stool
    public bool RemoveSeat(Transform seat)
    {
        if (IsSeatOccupied(seat)) return false;
        seats.Remove(seat);
        return true;
    }
}
