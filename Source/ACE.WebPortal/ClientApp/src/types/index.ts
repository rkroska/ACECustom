// --- Common Entity Types ---

export interface Character {
  guid: number;
  name: string;
  wcid: number;
  isOnline: boolean;
  isAdmin?: boolean;
}

export interface Player extends Character {
  accountId: number;
  accountName: string;
  shard: string;
}

// --- Stats & Progression ---

export interface StatDetail {
  innate: number;
  ranks: number;
  total: number;
}

export interface StatsData {
  attributes: {
    strength: StatDetail;
    endurance: StatDetail;
    quickness: StatDetail;
    coordination: StatDetail;
    focus: StatDetail;
    self: StatDetail;
  };
  vitals: {
    health: StatDetail;
    stamina: StatDetail;
    mana: StatDetail;
  };
  ratings: {
    emd: number | null;
    dr: number;
    cdr: number;
    damage: number;
    critDamage: number;
  };
  augmentations: Record<string, number>;
  bank: Record<string, number>;
  level: number;
  enlightenment: number;
}

// --- Inventory & Items ---

export interface InventoryItem {
  guid: number;
  containerGuid: number;
  weenieType: number;
  requiresBackpackSlot: boolean;
  isContainer: boolean;
  name: string;
  wcid: number;
  stackSize: number;
  isEquipped: boolean;
  iconId?: number;
  iconUnderlayId?: number;
  iconOverlayId?: number;
  iconOverlaySecondaryId?: number;
  itemType?: number;
  uiEffects?: number;
}

// --- Property & Server Management ---

export interface PropertyMetadata {
  id: number;
  name: string;
  type: string;
  linkedEnum?: string | null;
}

export interface ServerParamMetadata {
  name: string;
  type: string;
  description: string;
  defaultValue: string;
  currentValue: string;
  isSet: boolean;
}

export interface SkillData {
  name: string;
  sac: string;
  base: number;
  total: number | null;
  isUsable: boolean;
}

// --- Auth & API ---

export interface AuthUser {
  username: string;
  token: string;
  accessLevel: number;
  lastLogin: string;
}

export interface ApiError {
  message: string;
  code?: string;
  details?: any;
}

// --- Lookup Tables & Enum Metadata ---

export interface EnumListItem {
  name: string
  isLinked: boolean
}

export interface EnumValueMetadata {
  id: number | string
  name: string
  hexValue?: string | null
}

export interface EnumDetail {
  isFlags: boolean
  underlyingType: string
  values: EnumValueMetadata[]
  primaryProperty?: string | null
}
