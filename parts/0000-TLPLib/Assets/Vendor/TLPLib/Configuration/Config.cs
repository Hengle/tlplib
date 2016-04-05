﻿using System;
using System.Collections.Generic;
using com.tinylabproductions.TLPLib.Concurrent;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Formats.MiniJSON;
using com.tinylabproductions.TLPLib.Functional;
using UnityEngine;

namespace com.tinylabproductions.TLPLib.Configuration {
  /* See IConfig. */
  public class Config : ConfigBase {
    public abstract class ConfigError {
      public readonly string message;

      protected ConfigError(string message) { this.message = message; }

      public override string ToString() { return $"{nameof(ConfigError)}[{message}]"; }
    }

    /** Errors which happen because retrieval fails. */
    public class ConfigRetrievalError : ConfigError {
      public ConfigRetrievalError(string message) : base(message) {}
    }

    public class ConfigWWWError : ConfigRetrievalError {
      /* This is the reporting URL */
      public readonly string url;
      public readonly WWWError error;

      public ConfigWWWError(string url, WWWError error) : base($"WWW error (url={url}): {error.error}") {
        this.url = url;
        this.error = error;
      }
    }

    public class WrongContentType : ConfigRetrievalError {
      public readonly string url, expectedContentType, actualContentType;

      public WrongContentType(string url, string expectedContentType, string actualContentType) 
      : base(
        $"Expected 'Content-Type' in '{url}' to be '{expectedContentType}', but it was '{actualContentType}'"
      ) {
        this.url = url;
        this.expectedContentType = expectedContentType;
        this.actualContentType = actualContentType;
      }
    }

    /** Errors which happen because the developers screwed up config content. */
    public class ConfigContentError : ConfigError {
      public ConfigContentError(string message) : base(message) {}
    }

    public class ParsingError : ConfigContentError {
      public readonly string url, jsonString;

      public ParsingError(string url, string jsonString) : base(
        $"Cannot parse url '{url}' contents as JSON object:\n{jsonString}"
      ) {
        this.url = url;
        this.jsonString = jsonString;
      }
    }

    /**
     * Fetches JSON config from URL. Checks its content type before parsing.
     *
     * If reportUrl != null, uses that in error reports. One use of it is adding a
     * timestamp query string parameter to the request URL to avoid caching, but using
     * an url without timestamp when reporting errors to your error tracker, because 
     * otherwise one error can trigger a thousand errors because the url always changes.
     *
     * Throws WrongContentType if unexpected content type is found. 
     * Throws ParsingError if JSON could not be parsed,.
     **/
    public static Future<Either<ConfigError, IConfig>> apply(
      string fetchUrl, string reportUrl=null, string expectedContentType= "application/json"
    ) {
      reportUrl = reportUrl ?? fetchUrl;
      return new WWW(fetchUrl).wwwFuture().map(wwwE => wwwE.fold(
        err => Either<ConfigError, IConfig>.Left(new ConfigWWWError(reportUrl, err)),
        www => {
          var contentType = www.responseHeaders.get("CONTENT-TYPE").getOrElse("undefined");
          // Sometimes we get redirected to internet paygate, which returns HTML 
          // instead of our content.
          if (contentType != expectedContentType)
            return Either<ConfigError, IConfig>.Left(
              new WrongContentType(reportUrl, expectedContentType, contentType)
            );

          var json = (Dictionary<string, object>) Json.Deserialize(www.text);
          return json == null 
            ? Either<ConfigError, IConfig>.Left(new ParsingError(reportUrl, www.text)) 
            : Either<ConfigError, IConfig>.Right(new Config(json));
        })
      );
    }

    // Implementation

    delegate Option<A> Parser<A>(object node);

    static readonly Parser<Dictionary<string, object>> jsClassParser = 
      n => F.opt(n as Dictionary<string, object>);
    static readonly Parser<string> stringParser = n => F.some(n as string);
    static Option<A> castA<A>(object a) {
      return a is A ? F.some((A) a) : F.none<A>();
    }

    static readonly Parser<int> intParser = n => {
      if (n is long) return F.some((int) (long) n);
      else if (n is int) return F.some((int) n);
      else return Option<int>.None;
    };
    static readonly Parser<long> longParser = n => {
      if (n is long) return F.some((long) n);
      else if (n is int) return F.some((long) (int) n);
      else return Option<long>.None;
    };
    static readonly Parser<float> floatParser = n => {
      if (n is double) return F.some((float) (double) n);
      else if (n is float) return F.some((float) n);
      else if (n is long) return F.some((float) (long) n);
      else if (n is int) return F.some((float) (int) n);
      else return Option<float>.None;
    };
    static readonly Parser<bool> boolParser = n => castA<bool>(n);
    static readonly Parser<DateTime> dateTimeParser = 
      n => F.opt(n as string).flatMap(_ => _.parseDateTime().rightValue);

    public override string scope { get; }

    readonly Dictionary<string, object> root, scopedRoot;

    public Config(Dictionary<string, object> root, Dictionary<string, object> scopedRoot=null, string scope="") {
      this.scope = scope;
      this.root = root;
      this.scopedRoot = scopedRoot ?? root;
    }

    #region either getters

    public override Either<ConfigFetchError, string> eitherString(string key) 
    { return get(key, stringParser); }

    public override Either<ConfigFetchError, IList<string>> eitherStringList(string key) 
    { return getList(key, stringParser); }

    public override Either<ConfigFetchError, int> eitherInt(string key) 
    { return get(key, intParser); }

    public override Either<ConfigFetchError, IList<int>> eitherIntList(string key) 
    { return getList(key, intParser); }

    public override Either<ConfigFetchError, long> eitherLong(string key) 
    { return get(key, longParser); }

    public override Either<ConfigFetchError, IList<long>> eitherLongList(string key) 
    { return getList(key, longParser); }

    public override Either<ConfigFetchError, float> eitherFloat(string key) 
    { return get(key, floatParser); }

    public override Either<ConfigFetchError, IList<float>> eitherFloatList(string key) 
    { return getList(key, floatParser); }

    public override Either<ConfigFetchError, bool> eitherBool(string key) 
    { return get(key, boolParser); }

    public override Either<ConfigFetchError, IList<bool>> eitherBoolList(string key) 
    { return getList(key, boolParser); }

    public override Either<ConfigFetchError, DateTime> eitherDateTime(string key) 
    { return get(key, dateTimeParser); }

    public override Either<ConfigFetchError, IList<DateTime>> eitherDateTimeList(string key) 
    { return getList(key, dateTimeParser); }

    public override Either<ConfigFetchError, IConfig> eitherSubConfig(string key) 
    { return fetchSubConfig(key); }

    public override Either<ConfigFetchError, IList<IConfig>> eitherSubConfigList(string key) 
    { return fetchSubConfigList(key); }

    #endregion

    Either<ConfigFetchError, IConfig> fetchSubConfig(string key) {
      return get(key, jsClassParser).mapRight(n => 
        (IConfig) new Config(root, n, scope == "" ? key : scope + "." + key)
      );
    }

    Either<ConfigFetchError, IList<IConfig>> fetchSubConfigList(string key) {
      return getList(key, jsClassParser).mapRight(nList => {
        var lst = F.emptyList<IConfig>(nList.Count);
        // ReSharper disable once LoopCanBeConvertedToQuery
        for (var idx = 0; idx < nList.Count; idx++) {
          var n = nList[idx];
          lst.Add(new Config(root, n, $"{(scope == "" ? key : scope + "." + key)}[{idx}]"));
        }
        return (IList<IConfig>) lst;
      });
    }

    Either<ConfigFetchError, A> get<A>(string key, Parser<A> parser, Dictionary<string, object> current = null) {
      var parts = split(key);

      current = current ?? scopedRoot;
      foreach (var part in parts.dropRight(1)) {
        var either = fetch(current, key, part, jsClassParser);
        if (either.isLeft) return either.mapRight(_ => default(A));
        current = either.rightValue.get;
      }

      return fetch(current, key, parts[parts.Length - 1], parser);
    }

    static string[] split(string key) {
      return key.Split('.');
    }

    Either<ConfigFetchError, IList<A>> getList<A>(
      string key, Parser<A> parser
    ) {
      return get(key, n => F.some(n as List<object>)).flatMapRight(arr => {
        var list = new List<A>(arr.Count);
        for (var idx = 0; idx < arr.Count; idx++) {
          var node = arr[idx];
          var parsed = parser(node);
          if (parsed.isDefined) list.Add(parsed.get);
          else return F.left<ConfigFetchError, IList<A>>(ConfigFetchError.wrongType(
            $"Cannot convert '{key}'[{idx}] to {typeof (A)}: {node}"
          ));
        }
        return F.right<ConfigFetchError, IList<A>>(list);
      });
    }

    Either<ConfigFetchError, A> fetch<A>(
      Dictionary<string, object> current, string key, string part, Parser<A> parser
    ) {
      if (!current.ContainsKey(part)) 
        return F.left<ConfigFetchError, A>(ConfigFetchError.keyNotFound(
          $"Cannot find part '{part}' from key '{key}' in {current.asString()} " +
          $"[scope='{scope}']"
        ));
      var node = current[part];

      return followReference(node).flatMapRight(n => 
        parser(n).fold(
          () => F.left<ConfigFetchError, A>(ConfigFetchError.wrongType(
            $"Cannot convert part '{part}' from key '{key}' to {typeof (A)}. Type={n.GetType()}" +
            $" Contents: {n}"
          )), F.right<ConfigFetchError, A>
        )
      );
    }

    Either<ConfigFetchError, object> followReference(object current) {
      var str = current as string;
      // references are specified with '#REF=...#'
      if (
        str != null &&
        str.Length >= 6
        && str.Substring(0, 5) == "#REF="
        && str.Substring(str.Length - 1, 1) == "#"
      ) {
        var key = str.Substring(5, str.Length - 6);
        // References are always followed from the root tree.
        return get(key, F.some, root).mapLeft(err =>
          ConfigFetchError.brokenRef($"While following reference {str}: {err}")
        );
      }
      else return F.right<ConfigFetchError, object>(current);
    }

    public override string ToString() {
      return $"Config(scope: \"{scope}\", data: {scopedRoot})";
    }
  }
}