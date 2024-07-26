using System.Collections.Generic;
using System.Linq;

namespace PvpXray
{
    class ManifestTypeVerifierV3 : Verifier.IChecker
    {
        public static string[] Checks { get; } = { "PVP-107-3" };
        public static int PassCount => 0;

        readonly Verifier.Context m_Context;

        public ManifestTypeVerifierV3(Verifier.Context context)
        {
            m_Context = context;
            Manifest(context.Manifest);
        }

        void Manifest(Json manifest)
        {
            foreach (var m in Members(manifest))
            {
                switch (m.Key)
                {
                    case "_upm": Object(m); break;
                    case "author": Author(m); break;
                    case "category": String(m); break;
                    case "changelogUrl": String(m); break;
                    case "dependencies": ObjectOfString(m); break;
                    case "description": String(m); break;
                    case "displayName": String(m); break;
                    case "documentationUrl": String(m); break;
                    case "files": ArrayOfString(m); break;
                    case "hideInEditor": Boolean(m); break;
                    case "host": String(m); break;
                    case "keywords": ArrayOfString(m); break;
                    case "license": String(m); break;
                    case "name": String(m); break;
                    case "relatedPackages": ObjectOfString(m); break;
                    case "repository": Repository(m); break;
                    case "samples": Samples(m); break;
                    case "searchablePackages": ArrayOfString(m); break;
                    case "type": String(m); break;
                    case "unity": String(m); break;
                    case "unityRelease": String(m); break;
                    case "upmCi": ObjectOfString(m, "footprint"); break;
                    case "version": String(m); break;
                    default: Unknown(m); break;
                }
            }
        }

        void Author(Json author)
        {
            foreach (var m in Members(author))
            {
                switch (m.Key)
                {
                    case "email": String(m); break;
                    case "name": String(m); break;
                    case "url": String(m); break;
                    default: Unknown(m); break;
                }
            }
        }

        void Repository(Json repository)
        {
            foreach (var m in Members(repository))
            {
                switch (m.Key)
                {
                    case "footprint": String(m); break;
                    case "revision": String(m); break;
                    case "type": String(m); break;
                    case "url": String(m); break;
                    default: Unknown(m); break;
                }
            }
        }

        void Samples(Json samples)
        {
            foreach (var e in Elements(samples))
            {
                try
                {
                    String(e["description"]);
                    String(e["displayName"]);
                    String(e["path"]);
                }
                catch (SimpleJsonException ex)
                {
                    m_Context.AddError("PVP-107-3", ex.FullMessage);
                }

                foreach (var m in Members(e))
                {
                    switch (m.Key)
                    {
                        case "description": String(m); break;
                        case "displayName": String(m); break;
                        case "importPath": String(m); break;
                        case "interactiveImport": Boolean(m); break;
                        case "path": String(m); break;
                        default: Unknown(m); break;
                    }
                }
            }
        }

        void Object(Json value) => Members(value);

        void ObjectOfString(Json value, params string[] allowedKeys)
        {
            foreach (var m in Members(value))
            {
                if (allowedKeys.Length == 0) String(m);
                else
                {
                    if (allowedKeys.Contains(m.Key)) String(m);
                    else Unknown(m);
                }
            }
        }

        IEnumerable<Json> Members(Json value)
        {
            try
            {
                return value.Members;
            }
            catch (SimpleJsonException e)
            {
                m_Context.AddError("PVP-107-3", e.FullMessage);
                return Enumerable.Empty<Json>();
            }
        }

        void ArrayOfString(Json value)
        {
            foreach (var m in Elements(value))
            {
                String(m);
            }
        }

        IEnumerable<Json> Elements(Json value)
        {
            try
            {
                return value.Elements;
            }
            catch (SimpleJsonException e)
            {
                m_Context.AddError("PVP-107-3", e.FullMessage);
                return Enumerable.Empty<Json>();
            }
        }

        void String(Json value)
        {
            try
            {
                _ = value.String;
            }
            catch (SimpleJsonException e)
            {
                m_Context.AddError("PVP-107-3", e.FullMessage);
            }
        }

        void Boolean(Json value)
        {
            try
            {
                _ = value.Boolean;
            }
            catch (SimpleJsonException e)
            {
                m_Context.AddError("PVP-107-3", e.FullMessage);
            }
        }

        void Unknown(Json value)
        {
            m_Context.AddError("PVP-107-3", $"package.json: {value.Path}: property not allowed");
        }

        public void CheckItem(Verifier.PackageFile file, int passIndex) { }
        public void Finish() { }
    }
}
