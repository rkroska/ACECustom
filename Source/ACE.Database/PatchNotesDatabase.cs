using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Database.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace ACE.Database
{
    public class PatchNotesSearchCriteria
    {
        public string Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool PublishedOnly { get; set; } = true;

        public void Normalize()
        {
            Search = Search?.Trim();
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = 1;
            if (PageSize > 100) PageSize = 100;
        }

        /// <summary>Public list API must never honor client-supplied draft visibility.</summary>
        public void PrepareForPublicList()
        {
            PublishedOnly = true;
            Normalize();
        }
    }

    public static class PatchNotesDatabase
    {
        public static DateTime? GetLastPublishedAt()
        {
            using var context = new AuthDbContext();
            return context.PatchNotes
                .AsNoTracking()
                .Where(n => n.Status == PatchNoteStatus.Published && n.PublishedAt != null)
                .Max(n => (DateTime?)n.PublishedAt);
        }

        public static PagedResult<PatchNote> Search(PatchNotesSearchCriteria criteria)
        {
            criteria.Normalize();

            using var context = new AuthDbContext();
            var query = context.PatchNotes.AsNoTracking().AsQueryable();

            if (criteria.PublishedOnly)
                query = query.Where(n => n.Status == PatchNoteStatus.Published);

            if (!string.IsNullOrEmpty(criteria.Search))
            {
                var term = criteria.Search.ToLower();
                query = query.Where(n =>
                    n.Title.ToLower().Contains(term) ||
                    (n.Summary != null && n.Summary.ToLower().Contains(term)) ||
                    n.Body.ToLower().Contains(term) ||
                    n.Slug.ToLower().Contains(term));
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(n => n.PublishedAt ?? n.UpdatedAt)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize)
                .ToList();

            return new PagedResult<PatchNote>
            {
                Items = items,
                TotalCount = totalCount,
                Page = criteria.Page,
                PageSize = criteria.PageSize
            };
        }

        public static PatchNote GetBySlug(string slug, bool publishedOnly = true)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            using var context = new AuthDbContext();
            var query = context.PatchNotes.AsNoTracking().Where(n => n.Slug == slug);
            if (publishedOnly)
                query = query.Where(n => n.Status == PatchNoteStatus.Published);

            return query.FirstOrDefault();
        }

        public static PatchNote GetById(int id)
        {
            using var context = new AuthDbContext();
            return context.PatchNotes.FirstOrDefault(n => n.Id == id);
        }

        public static bool SlugExists(string slug, int? excludeId = null)
        {
            using var context = new AuthDbContext();
            var query = context.PatchNotes.AsNoTracking().Where(n => n.Slug == slug);
            if (excludeId.HasValue)
                query = query.Where(n => n.Id != excludeId.Value);

            return query.Any();
        }

        public static PatchNote Create(PatchNote note)
        {
            var now = DateTime.UtcNow;
            note.CreatedAt = now;
            note.UpdatedAt = now;

            using var context = new AuthDbContext();
            context.PatchNotes.Add(note);
            context.SaveChanges();
            return note;
        }

        public static PatchNote Update(PatchNote note)
        {
            note.UpdatedAt = DateTime.UtcNow;

            using var context = new AuthDbContext();
            context.PatchNotes.Update(note);
            context.SaveChanges();
            return note;
        }

        public static void Delete(int id)
        {
            using var context = new AuthDbContext();
            var note = context.PatchNotes.FirstOrDefault(n => n.Id == id);
            if (note == null)
                return;

            context.PatchNotes.Remove(note);
            context.SaveChanges();
        }
    }
}
