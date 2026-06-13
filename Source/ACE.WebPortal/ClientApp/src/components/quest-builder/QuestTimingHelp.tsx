export default function QuestTimingHelp({
  hasPickupStamp,
  hasStartStamp,
}: {
  hasPickupStamp: boolean
  hasStartStamp: boolean
}) {
  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-950/60 p-3 space-y-2 text-[10px] text-neutral-500 leading-relaxed">
      <div className="text-[10px] uppercase font-bold text-neutral-400">Stamp roles</div>
      {hasStartStamp && (
        <p>
          <strong className="text-neutral-400">Start stamp</strong> (Start → first talk): flags that the player was
          briefed. Landscape pickup can require this before giving the item.
        </p>
      )}
      <p>
        <strong className="text-neutral-400">Pickup stamp</strong> (Obtain → landscape gate): limits how often the player
        can take the item from the world object. Export writes a separate <span className="font-mono">quest</span> row.
      </p>
      <p>
        <strong className="text-neutral-400">Completion stamp</strong> (Turn in): limits how often the NPC accepts the
        item and gives the reward. Use a <em>different</em> name for each role.
      </p>
      {!hasPickupStamp && (
        <p>
          Corpse loot has no pickup stamp — the mob always drops the item. Only the turn-in stamp applies on hand-in.
        </p>
      )}
      <p>
        <strong className="text-neutral-400">Step delay</strong> on Tell lines is only a pause before the next emote in
        that flow.
      </p>
    </div>
  )
}
