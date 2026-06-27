import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Activity, Save, Shield, ShieldAlert } from 'lucide-react';
import PageHeader from './common/PageHeader';
import { api } from '../services/api';
import { useAuthStore } from '../store/useAuthStore';
import { getRoleName } from '../utils/auth';

type PortalPageRow = {
  key: string;
  label: string;
  route: string;
  section: string;
  minLevel: number;
};

type PortalPagesGetResponse = {
  pages: PortalPageRow[];
  canEdit?: boolean;
  hasCustomLevels?: boolean;
  storage?: string;
};

type PortalPagesPutResponse = PortalPagesGetResponse & {
  pageAccess?: Record<string, boolean>;
};

function roleLabel(level: number) {
  if (level <= 0) return 'Player';
  return getRoleName(level);
}

const PortalSecurity: React.FC = () => {
  const { canAccessPage, accessLevel, setPageAccess } = useAuthStore();
  const hasAccess = canAccessPage('portal-security');

  const [pages, setPages] = useState<PortalPageRow[]>([]);
  const [originalLevels, setOriginalLevels] = useState<Record<string, number>>({});
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [canEdit, setCanEdit] = useState(false);
  const [hasCustomLevels, setHasCustomLevels] = useState(false);

  const dirty = useMemo(() => {
    return pages.some(p => originalLevels[p.key] !== p.minLevel);
  }, [pages, originalLevels]);

  const fetchPages = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.get<PortalPagesGetResponse>('/api/portal-access/pages');
      if (!data?.pages) {
        setError('No portal pages returned by the server.');
        return;
      }

      const sorted = [...data.pages].sort((a, b) => a.key.localeCompare(b.key));
      setPages(sorted);
      setOriginalLevels(Object.fromEntries(sorted.map(p => [p.key, p.minLevel])));
      setCanEdit(!!data.canEdit);
      setHasCustomLevels(!!data.hasCustomLevels);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load portal access settings.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  const savePages = useCallback(async () => {
    if (!canEdit || !dirty) return;
    setIsSaving(true);
    setError(null);
    try {
      const levels: Record<string, number> = {};
      for (const p of pages) {
        if (originalLevels[p.key] !== p.minLevel) {
          levels[p.key] = p.minLevel;
        }
      }

      const data = await api.put<PortalPagesPutResponse>('/api/portal-access/pages', { levels });
      if (!data?.pages) {
        setError('Save succeeded but the server returned an unexpected response.');
        return;
      }

      const sorted = [...data.pages].sort((a, b) => a.key.localeCompare(b.key));
      setPages(sorted);
      setOriginalLevels(Object.fromEntries(sorted.map(p => [p.key, p.minLevel])));
      if (data.pageAccess) {
        setPageAccess(data.pageAccess);
      }
      setHasCustomLevels(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save portal access settings.');
    } finally {
      setIsSaving(false);
    }
  }, [canEdit, dirty, pages, originalLevels, setPageAccess]);

  useEffect(() => {
    if (hasAccess) fetchPages();
    else setIsLoading(false);
  }, [fetchPages, hasAccess]);

  const levelOptions = useMemo(
    () =>
      [0, 1, 2, 3, 4, 5].map(level => ({
        level,
        label: roleLabel(level),
      })),
    []
  );

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px] bg-neutral-900">
        <Activity className="w-8 h-8 text-blue-500 animate-spin" />
      </div>
    );
  }

  if (!hasAccess) return null;

  if (error && pages.length === 0) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-8 text-center bg-neutral-900 text-neutral-100">
        <ShieldAlert className="w-12 h-12 text-red-500 mb-4 opacity-50" />
        <h2 className="font-bold text-white mb-2 uppercase tracking-widest text-[10px]">Error Loading Portal Access</h2>
        <p className="text-neutral-500 text-sm max-w-xs font-medium">{error}</p>
        <button
          type="button"
          onClick={fetchPages}
          className="mt-6 px-4 py-2 bg-neutral-800 hover:bg-neutral-700 rounded-xl text-xs font-bold transition-all uppercase tracking-widest border border-neutral-700"
        >
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col min-h-0 bg-neutral-900 overflow-hidden text-neutral-100">
      <div className="p-8 pb-0 shrink-0">
        <div className="max-w-4xl mx-auto w-full">
          <PageHeader title="Portal security" icon={Shield}>
            {canEdit && (
              <button
                type="button"
                onClick={savePages}
                disabled={!dirty || isSaving}
                className={[
                  'px-3 py-1.5 rounded-full text-[10px] font-bold uppercase tracking-wider transition-all border flex items-center gap-1.5',
                  dirty && !isSaving
                    ? 'bg-blue-500/10 border-blue-500/20 text-blue-500 hover:bg-blue-500/15'
                    : 'bg-neutral-800 border-neutral-700 text-neutral-500 cursor-not-allowed',
                ].join(' ')}
              >
                {isSaving ? <Activity className="w-3 h-3 animate-spin" /> : <Save className="w-3 h-3" />}
                Save Changes
              </button>
            )}
          </PageHeader>

          <div className="text-neutral-500 text-xs font-medium leading-relaxed mb-2">
            Minimum access level per page (0 = all logged-in players, 4 = Developer by default for admin tools).
            Saves apply immediately for all connected portal users — no server restart or config file edit.
          </div>
          <div className="text-neutral-600 text-[10px] font-medium mb-4">
            {hasCustomLevels
              ? 'Using custom levels from ace_auth.portal_page_access.'
              : 'Using built-in defaults until you save changes.'}
          </div>
          {error && (
            <div className="mb-4 text-red-400 text-xs font-medium">{error}</div>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto custom-scrollbar p-8 pt-4">
        <div className="max-w-4xl mx-auto w-full">
          <div className="overflow-hidden rounded-2xl border border-neutral-800 bg-neutral-950/30">
            <table className="w-full text-left">
              <thead className="bg-neutral-950 border-b border-neutral-800">
                <tr>
                  <th className="px-4 py-3 text-[10px] font-black uppercase tracking-[0.2em] text-neutral-500">
                    Page
                  </th>
                  <th className="px-4 py-3 text-[10px] font-black uppercase tracking-[0.2em] text-neutral-500 w-56">
                    Minimum access
                  </th>
                </tr>
              </thead>
              <tbody>
                {pages.map((p) => (
                  <tr key={p.key} className="border-b border-neutral-800/60 last:border-b-0">
                    <td className="px-4 py-3">
                      <div className="flex flex-col">
                        <span className="text-sm font-semibold text-white">{p.label || p.key}</span>
                        <span className="text-[10px] text-neutral-600 font-bold uppercase tracking-widest">{p.key}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      {canEdit ? (
                        <select
                          value={p.minLevel}
                          onChange={(e) => {
                            const next = Number(e.target.value);
                            setPages((prev) => prev.map(x => (x.key === p.key ? { ...x, minLevel: next } : x)));
                          }}
                          className="w-full bg-neutral-950 border border-neutral-800 rounded-xl px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-blue-600/20 focus:border-blue-600 transition-all font-medium text-sm"
                        >
                          {levelOptions.map(opt => (
                            <option key={opt.level} value={opt.level}>
                              {opt.level} — {opt.label}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <div className="px-3 py-2 rounded-xl border border-neutral-800 bg-neutral-950 text-sm font-medium text-neutral-300">
                          {p.minLevel} — {roleLabel(p.minLevel)}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}

                {pages.length === 0 && (
                  <tr>
                    <td colSpan={2} className="px-4 py-12 text-center text-neutral-600 text-sm font-medium">
                      No portal pages were returned by the server.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {!canEdit && accessLevel !== null && accessLevel < 5 && (
            <div className="mt-4 text-neutral-600 text-[10px] font-bold uppercase tracking-[0.2em]">Read-only</div>
          )}
        </div>
      </div>
    </div>
  );
};

export default PortalSecurity;
