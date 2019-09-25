using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Translate;
using Amazon.Translate.Model;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Nybus;
using QueueProcessor.Handlers;
using QueueProcessor.Messages;
using WorldDomination.Net.Http;

namespace Tests.Explicit.Handlers
{
    [TestFixture]
    public class SingleTranslateCommandHandlerTests
    {
        private string HtmlFormat = @"<html><body><div class=""lcb-body""><p>{0}</p></div></body></html>";

        private IFixture _fixture;

        private Mock<IAmazonTranslate> _mockTranslate;
        private Mock<IAmazonS3> _mockS3;
        private Mock<IDispatcher> _mockDispatcher;

        [SetUp]
        public void Initialize()
        {
            _fixture = new Fixture();

            _mockTranslate = new Mock<IAmazonTranslate>();

            _mockS3 = new Mock<IAmazonS3>();

            _mockDispatcher = new Mock<IDispatcher>();
        }

        [Test]
        public void HttpClient_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new SingleTranslateCommandHandler(null, Mock.Of<IAmazonTranslate>(), 
                Mock.Of<IAmazonS3>(), Mock.Of<IOptions<TranslateOptions>>(), Mock.Of<ILogger<SingleTranslateCommandHandler>>()));
        }

        [Test]
        public void AmazonTranslate_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new SingleTranslateCommandHandler(new HttpClient(), null,
                Mock.Of<IAmazonS3>(), Mock.Of<IOptions<TranslateOptions>>(), Mock.Of<ILogger<SingleTranslateCommandHandler>>()));
        }

        [Test]
        public void AmazonS3_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new SingleTranslateCommandHandler(new HttpClient(), Mock.Of<IAmazonTranslate>(),
                null, Mock.Of<IOptions<TranslateOptions>>(), Mock.Of<ILogger<SingleTranslateCommandHandler>>())) ;
        }

        [Test]
        public void TranslateOptions_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new SingleTranslateCommandHandler(new HttpClient(), Mock.Of<IAmazonTranslate>(),
               Mock.Of<IAmazonS3>(), null, Mock.Of<ILogger<SingleTranslateCommandHandler>>()));
        }

        [Test]
        public void Logger_is_required()
        {
            Assert.Throws<ArgumentNullException>(() => new SingleTranslateCommandHandler(new HttpClient(), Mock.Of<IAmazonTranslate>(),
              Mock.Of<IAmazonS3>(), Mock.Of<IOptions<TranslateOptions>>(), null));
        }

        private SingleTranslateCommandHandler CreateSystemUnderTest(TranslateOptions options, params HttpMessageOptions[] httpOptions)
        {
            var handler = new FakeHttpMessageHandler(httpOptions);
            var http = new HttpClient(handler);

            var wrappedOptions = new OptionsWrapper<TranslateOptions>(options);

            return new SingleTranslateCommandHandler(http, _mockTranslate.Object, _mockS3.Object, wrappedOptions, Mock.Of<ILogger<SingleTranslateCommandHandler>>());
        }

        [Test]
        public void HandleAsync_throws_if_Italian_translation()
        {
            var httpOption = new HttpMessageOptions
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var options = _fixture.Create<TranslateOptions>();

            var sut = CreateSystemUnderTest(options, httpOption);

            var context = _fixture.Create<NybusCommandContext<TranslateEducationCommand>>();
            context.Command.ToLanguage = Language.Italian;

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.HandleAsync(_mockDispatcher.Object, context));
        }

        [Test]
        public async Task HandleAsync_downloads_the_proper_Education_profile()
        {
            // ARRANGE
            
            var text = string.Format(this.HtmlFormat, _fixture.Create<string>());

            var httpOption = new HttpMessageOptions
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(text)
                }
            };

            var options = _fixture.Create<TranslateOptions>();

            _mockTranslate.Setup(e => e.TranslateTextAsync(It.IsAny<TranslateTextRequest>(), CancellationToken.None))
                .ReturnsAsync(_fixture.Create<TranslateTextResponse>());
            //.ReturnsAsync<TranslateTextResponse>(_fixture.Create<TranslateTextResponse>());

            _mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None));

            var sut = CreateSystemUnderTest(options, httpOption);

            var context = _fixture.Create<NybusCommandContext<TranslateEducationCommand>>();

            // continue...

            // ACT

            await sut.HandleAsync(_mockDispatcher.Object, context);

            // ASSERT

            Assert.That(httpOption.HttpResponseMessage.RequestMessage.RequestUri.ToString(), 
                Is.EqualTo(string.Format(SingleTranslateCommandHandler.EducationProfileFormat, context.Command.EducationId)));
        }

        [Test]
        public async Task HandleAsync_uses_Amazon_Translate_to_translate_text()
        {
            // ARRANGE
            var text = string.Format(this.HtmlFormat, _fixture.Create<string>());

            var httpOption = new HttpMessageOptions
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(text)
                }
            };

            var options = _fixture.Create<TranslateOptions>();
            var translation = _fixture.Create<TranslateTextResponse>();

            _mockTranslate.Setup(e => e.TranslateTextAsync(It.IsAny<TranslateTextRequest>(), CancellationToken.None))
                .ReturnsAsync(translation);

            _mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None));

            var sut = CreateSystemUnderTest(options, httpOption);

            var context = _fixture.Create<NybusCommandContext<TranslateEducationCommand>>();

            // ACT
            await sut.HandleAsync(_mockDispatcher.Object, context);

            // ASSERT
            _mockTranslate.Verify(e => e.TranslateTextAsync(It.IsAny<TranslateTextRequest>(), CancellationToken.None));

        }

        [Test]
        public async Task HandleAsync_uses_Amazon_S3_to_store_translations()
        {
            // ARRANGE
            var text = string.Format(this.HtmlFormat, _fixture.Create<string>());

            var httpOption = new HttpMessageOptions
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(text)
                }
            };

            var options = _fixture.Create<TranslateOptions>();
            var translation = _fixture.Create<TranslateTextResponse>();

            _mockTranslate.Setup(e => e.TranslateTextAsync(It.IsAny<TranslateTextRequest>(), CancellationToken.None))
                .ReturnsAsync(translation);

            _mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None));

            var sut = CreateSystemUnderTest(options, httpOption);

            var context = _fixture.Create<NybusCommandContext<TranslateEducationCommand>>();

            // ACT
            await sut.HandleAsync(_mockDispatcher.Object, context);

            // ASSERT
            _mockS3.Verify(e => e.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None));
        }

        [Test]
        public async Task HandleAsync_raises_event_when_completed()
        {
            // ARRANGE
            var text = string.Format(this.HtmlFormat, _fixture.Create<string>());

            var httpOption = new HttpMessageOptions
            {
                HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(text)
                }
            };

            var options = _fixture.Create<TranslateOptions>();
            var translation = _fixture.Create<TranslateTextResponse>();

            _mockTranslate.Setup(e => e.TranslateTextAsync(It.IsAny<TranslateTextRequest>(), CancellationToken.None))
                .ReturnsAsync(translation);

            _mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None));

            var sut = CreateSystemUnderTest(options, httpOption);

            var context = _fixture.Create<NybusCommandContext<TranslateEducationCommand>>();

            // ACT
            await sut.HandleAsync(_mockDispatcher.Object, context);

            // ASSERT
            _mockDispatcher.Verify(e => e.RaiseEventAsync(It.IsAny<EducationTranslatedEvent>(), It.IsAny<IDictionary<string,string>>()));
        }
    }
}