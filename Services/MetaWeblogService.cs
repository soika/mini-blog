namespace mini_blog
{
    using System;
    using System.Linq;
    using System.Security.Claims;
    using Controllers;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using WilderMinds.MetaWeblog;

    public class MetaWeblogService : IMetaWeblogProvider
    {
        private readonly IBlogService blog;
        private readonly IConfiguration config;
        private readonly IHttpContextAccessor context;

        public MetaWeblogService(IBlogService blog, IConfiguration config, IHttpContextAccessor context)
        {
            this.blog = blog;
            this.config = config;
            this.context = context;
        }

        public string AddPost(string blogid, string username, string password, Post post, bool publish)
        {
            ValidateUser(username, password);

            var newPost = new Models.Post
            {
                Title = post.title,
                Slug = !string.IsNullOrWhiteSpace(post.wp_slug) ? post.wp_slug : Models.Post.CreateSlug(post.title),
                Content = post.description,
                IsPublished = publish,
                Categories = post.categories
            };

            if (post.dateCreated != DateTime.MinValue)
            {
                newPost.PubDate = post.dateCreated;
            }

            this.blog.SavePost(newPost).GetAwaiter().GetResult();

            return newPost.Id;
        }

        public bool DeletePost(string key, string postid, string username, string password, bool publish)
        {
            ValidateUser(username, password);

            var post = this.blog.GetPostById(postid).GetAwaiter().GetResult();

            if (post != null)
            {
                this.blog.DeletePost(post).GetAwaiter().GetResult();
                return true;
            }

            return false;
        }

        public bool EditPost(string postid, string username, string password, Post post, bool publish)
        {
            ValidateUser(username, password);

            var existing = this.blog.GetPostById(postid).GetAwaiter().GetResult();

            if (existing != null)
            {
                existing.Title = post.title;
                existing.Slug = post.wp_slug;
                existing.Content = post.description;
                existing.IsPublished = publish;
                existing.Categories = post.categories;

                if (post.dateCreated != DateTime.MinValue)
                {
                    existing.PubDate = post.dateCreated;
                }

                this.blog.SavePost(existing).GetAwaiter().GetResult();

                return true;
            }

            return false;
        }

        public CategoryInfo[] GetCategories(string blogid, string username, string password)
        {
            ValidateUser(username, password);

            return this.blog.GetCategories().GetAwaiter().GetResult()
                .Select(cat =>
                    new CategoryInfo
                    {
                        categoryid = cat,
                        title = cat
                    })
                .ToArray();
        }

        public Post GetPost(string postid, string username, string password)
        {
            ValidateUser(username, password);

            var post = this.blog.GetPostById(postid).GetAwaiter().GetResult();

            if (post != null)
            {
                return ToMetaWebLogPost(post);
            }

            return null;
        }

        public Post[] GetRecentPosts(string blogid, string username, string password, int numberOfPosts)
        {
            ValidateUser(username, password);

            return this.blog.GetPosts(numberOfPosts).GetAwaiter().GetResult().Select(p => ToMetaWebLogPost(p))
                .ToArray();
        }

        public BlogInfo[] GetUsersBlogs(string key, string username, string password)
        {
            ValidateUser(username, password);

            var request = this.context.HttpContext.Request;
            var url = request.Scheme + "://" + request.Host;

            return new[]
            {
                new BlogInfo
                {
                    blogid = "1",
                    blogName = this.config["blog:name"],
                    url = url
                }
            };
        }

        public MediaObjectInfo NewMediaObject(string blogid, string username, string password, MediaObject mediaObject)
        {
            ValidateUser(username, password);
            var bytes = Convert.FromBase64String(mediaObject.bits);
            var path = this.blog.SaveFile(bytes, mediaObject.name).GetAwaiter().GetResult();

            return new MediaObjectInfo {url = path};
        }

        public UserInfo GetUserInfo(string key, string username, string password)
        {
            ValidateUser(username, password);
            throw new NotImplementedException();
        }

        public int AddCategory(string key, string username, string password, NewCategory category)
        {
            ValidateUser(username, password);
            throw new NotImplementedException();
        }

        private void ValidateUser(string username, string password)
        {
            if (username != this.config["user:username"] ||
                !AccountController.VerifyHashedPassword(password, this.config))
            {
                throw new MetaWeblogException("Unauthorized");
            }

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, this.config["user:username"]));

            this.context.HttpContext.User = new ClaimsPrincipal(identity);
        }

        private Post ToMetaWebLogPost(Models.Post post)
        {
            var request = this.context.HttpContext.Request;
            var url = request.Scheme + "://" + request.Host;

            return new Post
            {
                postid = post.Id,
                title = post.Title,
                wp_slug = post.Slug,
                permalink = url + post.GetLink(),
                dateCreated = post.PubDate,
                description = post.Content,
                categories = post.Categories.ToArray()
            };
        }
    }
}