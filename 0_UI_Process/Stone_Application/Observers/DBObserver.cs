using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stone_Application.Event;
using Stone_Application.Repository;

namespace Stone_Application.Observer
{
    public class DBObserver<T> : IEventObserver<T> where T : IInformation
    {
        private readonly IRepository<T> sqlServerRepository;

        public DBObserver (IRepository<T> sqlInstance)
        {
            sqlServerRepository = sqlInstance;
        }

        public void Update(T information)
        {
            sqlServerRepository.add(information);
        }
    }
}
