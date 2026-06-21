import {
  LucideIcon,
  User,
  Users,
  Globe,
  Swords,
  Search,
  Book,
  Package,
  ScrollText,
  GitBranch,
  FileCode,
  Terminal,
  Settings,
  Calendar,
  Shield,
  Trophy,
  ClipboardList,
  FileText,
  Skull,
} from 'lucide-react'

export interface PortalRouteDefinition {
  key: string
  path: string
  label: string
  section?: string
  icon?: LucideIcon
  /** When true, route is registered but has no real page yet */
  placeholder?: boolean
}

export const PORTAL_ROUTES: PortalRouteDefinition[] = [
  { key: 'characters', path: '/characters', label: 'Characters', icon: User },
  { key: 'leaderboards', path: '/leaderboards', label: 'Leaderboards', section: 'Player', icon: Trophy },
  { key: 'patch-notes', path: '/patch-notes', label: 'Patch Notes', section: 'Player', icon: FileText },
  { key: 'players', path: '/players', label: 'Player List', section: 'Monitoring', icon: Users },
  { key: 'corpse-finder', path: '/corpse-finder', label: 'Corpse Finder', section: 'Monitoring', icon: Skull },
  { key: 'audit-log', path: '/audit', label: 'Audit Log', section: 'Monitoring', icon: ClipboardList },
  { key: 'map', path: '/map', label: 'World Map', section: 'Monitoring', icon: Globe, placeholder: true },
  { key: 'combat-calculator', path: '/combat-calculator', label: 'Combat Calculator', section: 'Content Tools', icon: Swords },
  { key: 'properties', path: '/properties', label: 'Property Explorer', section: 'Content Tools', icon: Search },
  { key: 'lookup', path: '/lookup', label: 'Lookup Tables', section: 'Content Tools', icon: Book },
  { key: 'items', path: '/items', label: 'Item Search', section: 'Content Tools', icon: Package },
  { key: 'stamps', path: '/stamps', label: 'Stamp Search', section: 'Content Tools', icon: ScrollText },
  { key: 'quest-builder', path: '/quest-builder', label: 'Quest Builder', section: 'Content Tools', icon: GitBranch },
  { key: 'weenie', path: '/weenie', label: 'Weenie Editor', section: 'Content Tools', icon: FileCode, placeholder: true },
  { key: 'console', path: '/console', label: 'Console', section: 'Server Management', icon: Terminal, placeholder: true },
  { key: 'params', path: '/params', label: 'Server Params', section: 'Server Management', icon: Settings },
  { key: 'events', path: '/events', label: 'Server Events', section: 'Server Management', icon: Calendar },
  { key: 'portal-security', path: '/portal-security', label: 'Portal Security', section: 'Server Management', icon: Shield },
  { key: 'patch-notes-admin', path: '/patch-notes/manage', label: 'Manage Patch Notes', section: 'Server Management', icon: FileText },
]

