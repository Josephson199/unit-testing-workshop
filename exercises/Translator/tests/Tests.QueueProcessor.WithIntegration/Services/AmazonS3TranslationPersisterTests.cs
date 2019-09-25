using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using AutoFixture.Idioms;
using AutoFixture.NUnit3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using QueueProcessor.Handlers;
using QueueProcessor.Services;

namespace Tests.Implicit.Services
{
    [TestFixture]
    public class AmazonS3TranslationPersisterTests
    {
        [Test, MyAutoData]
        public void Constructor_is_guarded(GuardClauseAssertion assertion)
        {
            assertion.Verify(typeof(AmazonS3TranslationPersister).GetConstructors());
        }

        [Test, MyAutoData]
        public async Task PersistTranslations_pushes_to_s3([Frozen]IAmazonS3 amazonS3, IOptions<TranslateOptions> options, 
            ILogger<AmazonS3TranslationPersister> logger, IReadOnlyList<string> list, AmazonS3TranslationPersister sut)
        {
            await sut.PersistTranslations(It.IsAny<string>(), list);

            Mock.Get(amazonS3).Verify(a=>a.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None));
        }
    }
}