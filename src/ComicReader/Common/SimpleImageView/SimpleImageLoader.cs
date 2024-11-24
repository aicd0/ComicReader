// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace ComicReader.Common.SimpleImageView;

internal static class SimpleImageLoader
{
    public sealed class Transaction : BaseTransaction<TaskException>
    {
        private readonly CancellationSession.IToken _sessionToken;
        private readonly List<Token> _tokens;
        private TaskQueue _queue = TaskQueue.DefaultQueue;

        public Transaction(CancellationSession.IToken token, List<Token> tokens)
        {
            _sessionToken = token;
            _tokens = tokens;
        }

        public Transaction SetQueue(TaskQueue queue)
        {
            ArgumentNullException.ThrowIfNull(queue);

            _queue = queue;
            return this;
        }

        protected override TaskException CommitImpl()
        {
            _queue.Enqueue("SimpleImageLoader", delegate
            {
                foreach (Token token in _tokens)
                {
                    SimpleImageView.Model model = token.Model;
                    ImageCacheManager.LoadImage(_sessionToken, model.Source, model.FrameWidth, model.FrameHeight, model.StretchMode, token.ImageResultHandler);
                }
                return TaskException.Success;
            });
            return TaskException.Success;
        }
    }

    public class Token
    {
        public SimpleImageView.Model Model { get; set; } = new();
        public IImageResultHandler ImageResultHandler { get; set; }
    }
}
