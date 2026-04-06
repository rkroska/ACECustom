import { useState, useEffect, useMemo } from 'react'
import { Package, Shield, Activity } from 'lucide-react'
import { api } from '../../services/api'
import { InventoryItem } from '../../types'
import InventorySection from './InventorySection'
import InventoryHeader from './InventoryHeader'

interface CharacterInventoryProps {
  guid: number
}

export default function CharacterInventory({ guid }: CharacterInventoryProps) {
  const [items, setItems] = useState<InventoryItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [searchTerm, setSearchTerm] = useState('')
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({ 'main': true, 'equipped': true })

  useEffect(() => {
    fetchInventory()
  }, [guid])

  const fetchInventory = async () => {
    try {
      setIsLoading(true)
      const data = await api.get<InventoryItem[]>(`/api/character/inventory/${guid}`)
      setItems(data)
      
      const initialExpanded: Record<string, boolean> = { 'main': true, 'equipped': true }
      data.forEach((item: InventoryItem) => {
        if (item.requiresBackpackSlot && item.containerGuid === guid) {
          initialExpanded[`group-${item.guid}`] = true
        }
      })
      setExpandedSections(initialExpanded)
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }

  const toggleSection = (id: string) => {
    setExpandedSections(prev => ({ ...prev, [id]: !prev[id] }))
  }

  const { equipped, mainPackItems, topLevelHeaders, itemsByContainer, visibleIds } = useMemo(() => {
    const equipped: InventoryItem[] = []
    const mainPackItems: InventoryItem[] = []
    const topLevelHeaders: InventoryItem[] = []
    const itemsByContainer: Record<number, InventoryItem[]> = {}
    const itemsById: Record<number, InventoryItem> = {}
    const visibleIds = new Set<number>()

    items.forEach(item => {
      itemsById[item.guid] = item
      if (!itemsByContainer[item.containerGuid]) {
        itemsByContainer[item.containerGuid] = []
      }
      itemsByContainer[item.containerGuid].push(item)
    })

    const term = searchTerm.toLowerCase()
    const matches = (item: InventoryItem) => {
      if (!searchTerm) return true
      return item.name.toLowerCase().includes(term) ||
             item.wcid.toString().includes(term) ||
             item.guid.toString().includes(term)
    }

    items.forEach(item => {
      if (matches(item)) {
        visibleIds.add(item.guid)
        let parentGuid = item.containerGuid
        while (parentGuid && parentGuid !== guid) {
          if (visibleIds.has(parentGuid)) break;
          visibleIds.add(parentGuid)
          const parent = itemsById[parentGuid]
          if (!parent) break;
          parentGuid = parent.containerGuid
        }
      }
    })

    items.forEach(item => {
      if (!visibleIds.has(item.guid)) return

      if (item.isEquipped) {
        equipped.push(item)
      } else {
        if (item.containerGuid === guid) {
          if (item.requiresBackpackSlot) {
            topLevelHeaders.push(item)
          } else {
            mainPackItems.push(item)
          }
        }
      }
    })

    let finalTopLevel = topLevelHeaders
    let finalMainPack = mainPackItems
    if (searchTerm) {
      finalTopLevel = topLevelHeaders.filter(header => {
        const children = itemsByContainer[header.guid] || []
        return children.some(child => visibleIds.has(child.guid))
      })
      finalMainPack = mainPackItems.filter(item => visibleIds.has(item.guid))
    }

    equipped.sort((a, b) => a.name.localeCompare(b.name))
    finalMainPack.sort((a, b) => a.name.localeCompare(b.name))
    finalTopLevel.sort((a, b) => a.name.localeCompare(b.name))

    return { equipped, mainPackItems: finalMainPack, topLevelHeaders: finalTopLevel, itemsByContainer, visibleIds }
  }, [items, searchTerm, guid])

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Activity className="w-8 h-8 text-blue-500 animate-spin opacity-50" />
      </div>
    )
  }

  return (
    <div className="space-y-6 max-w-4xl mx-auto px-4 pb-20 relative">
      <InventoryHeader 
        searchTerm={searchTerm} 
        setSearchTerm={setSearchTerm} 
        totalCount={items.length} 
      />

      <div className="space-y-4">
        {/* Equipped Section Header */}
        {equipped.length > 0 && (
          <InventorySection
            title="Equipped Items"
            icon={<Shield className="w-3.5 h-3.5" />}
            items={equipped}
            isExpanded={expandedSections['equipped']}
            onToggle={() => toggleSection('equipped')}
          />
        )}

        {/* Main Pack Section Header */}
        {(mainPackItems.length > 0 || !searchTerm) && (
          <InventorySection
            title="Main Pack"
            icon={<Package className="w-3.5 h-3.5 text-amber-400" />}
            items={mainPackItems}
            isExpanded={expandedSections['main']}
            onToggle={() => toggleSection('main')}
            placeholder="Empty Pack"
          />
        )}

        {/* Containers & Top-Level Items */}
        <div className="space-y-4">
          {topLevelHeaders.map(header => {
            const children = (itemsByContainer[header.guid] || []).filter(c => visibleIds.has(c.guid))
            const sectionId = `group-${header.guid}`
            return (
              <InventorySection
                key={header.guid}
                title={header.name}
                icon={<Package className="w-3.5 h-3.5" />}
                items={children}
                isExpanded={expandedSections[sectionId]}
                onToggle={() => toggleSection(sectionId)}
                headerItem={header}
              />
            )
          })}
        </div>
      </div>

      {visibleIds.size === 0 && (
        <div className="text-center py-24 bg-neutral-950/20 border border-neutral-800/40 border-dashed rounded-3xl">
          <Package className="w-12 h-12 mx-auto mb-4 text-neutral-800/50" />
          <p className="text-neutral-500 font-medium tracking-tight">No items found matching your search</p>
        </div>
      )}
    </div>
  )
}
