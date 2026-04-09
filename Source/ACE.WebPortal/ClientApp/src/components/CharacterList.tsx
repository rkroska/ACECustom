import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Activity, Users, Shield, User } from 'lucide-react'
import { api } from '../services/api'
import { Character } from '../types'
import CharacterListItem from './common/CharacterListItem'
import PageHeader from './common/PageHeader'
import Pagination from './common/Pagination'

const ITEMS_PER_PAGE = 25

export default function CharacterList() {
  const navigate = useNavigate()
  const [characters, setCharacters] = useState<Character[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [currentPage, setCurrentPage] = useState(1)

  useEffect(() => {
    fetchCharacters()
  }, [])

  const fetchCharacters = async () => {
    try {
      setIsLoading(true)
      const data = await api.get<Character[]>('/api/character/list')
      setCharacters(data ?? [])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    } finally {
      setIsLoading(false)
    }
  }

  const totalPages = Math.ceil(characters.length / ITEMS_PER_PAGE)
  const paginatedCharacters = characters.slice(
    (currentPage - 1) * ITEMS_PER_PAGE,
    currentPage * ITEMS_PER_PAGE
  )

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px]">
        <Activity className="w-8 h-8 text-blue-500 animate-spin" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex-1 flex items-center justify-center text-red-400 p-8 text-center bg-neutral-900">
        <div>
          <Shield className="w-12 h-12 mx-auto mb-4 opacity-20 text-red-500" />
          <h2 className="text-xl font-bold mb-2 uppercase tracking-widest text-white">Error Loading Characters</h2>
          <p className="text-sm opacity-80">{error}</p>
        </div>
      </div>
    )
  }

  return (
    <div className="flex-1 flex flex-col min-h-0 bg-neutral-900 overflow-hidden text-neutral-100">
      {/* Header Container */}
      <div className="p-8 pb-0 shrink-0">
        <div className="max-w-2xl mx-auto w-full">
          <PageHeader title="Your characters" icon={Users}>
            <div className="hidden sm:flex px-3 py-1.5 bg-blue-600/10 border border-blue-500/20 rounded-full items-center gap-2 text-blue-400 text-[10px] font-bold uppercase tracking-wider">
              <Users className="w-3.5 h-3.5" />
              {characters.length} Registered
            </div>
          </PageHeader>
        </div>
      </div>

      {/* Main List Container (Scrollable) */}
      <div className="flex-1 overflow-y-auto custom-scrollbar p-8 pt-4">
        <div className="max-w-2xl mx-auto w-full flex flex-col gap-1.5">
          {paginatedCharacters.map(char => (
            <CharacterListItem 
              key={char.guid}
              character={char}
              onClick={() => navigate(`/characters/${char.guid}/general`)}
              locationInfo={char.location?.name || char.location?.hex}
              secondaryInfo={char.location?.coordinates}
            />
          ))}

          {characters.length === 0 && (
            <div className="flex flex-col items-center justify-center p-12 bg-neutral-950/50 border border-neutral-800 border-dashed rounded-3xl text-center">
              <div className="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center text-neutral-700 mb-4">
                <User className="w-8 h-8" />
              </div>
              <h3 className="text-white font-bold text-lg uppercase tracking-widest">No Characters Found</h3>
              <p className="text-neutral-500 text-sm max-w-xs mt-2 font-medium">
                No characters were found matching your account.
              </p>
            </div>
          )}
        </div>
      </div>

      {/* Pagination Footer */}
      <Pagination 
        currentPage={currentPage} 
        totalPages={totalPages} 
        onPageChange={setCurrentPage} 
      />
    </div>
  )
}
