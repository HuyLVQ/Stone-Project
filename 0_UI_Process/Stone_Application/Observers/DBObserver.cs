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
        private readonly IRepository<T, IResultInformation> m_sqlServerRepository;

        public DBObserver (IRepository<T, IResultInformation> p_sqlInstance)
        {
            m_sqlServerRepository = p_sqlInstance;
        }

        public void Update(T p_information)
        {
            m_sqlServerRepository.add(p_information);
        }
    }
}
