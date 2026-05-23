# Network & Mass Spawn Mitigations

Live-tunable server properties for client/server load near large spawns (e.g. 500+ mobs). Use `/showprops` to list all names.

---

## Network pacing (implemented)

| Property | Default | Description | Live command |
|----------|---------|-------------|--------------|
| `net_max_packets_per_tick` | 50 | Max UDP packets sent to one client per server tick | `/modifylong net_max_packets_per_tick 30` |
| `net_min_bundle_interval_ms` | 5 | Min ms between outbound bundle flushes per session | `/modifylong net_min_bundle_interval_ms 10` |
| `net_retransmit_warn_threshold` | 75 | Log RETRANSMIT when ACK prunes more than N cached packets | `/modifylong net_retransmit_warn_threshold 20` |

These apply to **all** outbound message types in a bundle, not CreateObject alone.

---

## Server-side throttles (implemented)

| Property | Default | Description | Live command |
|----------|---------|-------------|--------------|
| `monster_tick_throttle_limit` | 75 | Monsters processed per tick per landblock | `/modifylong monster_tick_throttle_limit 50` |
| `action_queue_throttle_limit` | 300 | Actions processed per tick | `/modifylong action_queue_throttle_limit 150` |

---

## Suggested values for mass spawns (500+ mobs)

```bash
/modifylong net_max_packets_per_tick 30
/modifylong net_min_bundle_interval_ms 10
/modifylong monster_tick_throttle_limit 50
/modifylong action_queue_throttle_limit 150
```

---

## Not implemented (do not use — no ServerConfig entry)

The following were design notes only; they are **not** wired in `PropertyManager` or `NetworkSession`:

- `network_create_object_max_per_tick`
- `network_update_position_max_per_tick`
- `network_movement_dedupe_window_ms`
- `network_telemetry_log_interval_seconds`
- `network_packet_pacing_enabled` / `network_priority_drain_enabled`

---

## Related files

- `Source/ACE.Server/Network/NetworkSession.cs` — packet pacing
- `Source/ACE.Server/Managers/PropertyManager.cs` — `net_*`, `monster_tick_throttle_limit`, `action_queue_throttle_limit`
