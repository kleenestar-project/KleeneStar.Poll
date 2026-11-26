using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Poll.WebPoll;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Poll.WWW.Api._1_.Polls._workspacekey_
{
    /// <summary>
    /// The poll classes of the workspace the caller may create in: the class selection of the
    /// dialog creating a poll.
    /// </summary>
    [Title("kleenestar.poll:poll.class.label")]
    [Cache]
    public sealed class Classes : RestApiSelection<Class>
    {
        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Class> query, IQueryContext context, IRequest request)
        {
            var workspace = PollRequest.Workspace(request);
            var classes = PollRequest.Classes(workspace).Where(x => PollAuthorization.MayCreate(x, request));

            return query.Apply(classes.AsQueryable())
                .Select(x => new RestApiSelectionItem { Id = x.Id, Text = x.Name })
                .ToList()
                .AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Class> Filter(string filter, IQuery<Class> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null" ? query : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }
}
