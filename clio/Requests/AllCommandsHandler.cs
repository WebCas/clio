//using MediatR;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Clio.Requests {


//	internal class AllOptionsRequestHandler : IRequestHandler<AllOptionsRequest<EnvironmentOptions>> {

//		public AllOptionsRequestHandler() {

//		}

//		public async Task<Unit> Handle(AllOptionsRequest<EnvironmentOptions> request, CancellationToken cancellationToken) {

//			var mi = request.Command.GetType().GetMethod("Execute");




//			_ = mi.Invoke(request.Command, new[] { request.Arguments });
//			return new Unit();
//		}
//	}





//}
