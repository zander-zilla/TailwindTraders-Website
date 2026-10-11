import React from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";

// react-router 6/7 removed withRouter and route props. This HOC gives the (class) components
// the v4-style props they were written against: history.push, match.params, location.
export default function withRouter(Component) {
  function WithRouter(props) {
    const navigate = useNavigate();
    const location = useLocation();
    const params = useParams();
    const history = {
      push: (to) => (typeof to === "string" ? navigate(to) : navigate(to.pathname, { state: to.state })),
      goBack: () => navigate(-1),
    };
    return <Component {...props} navigate={navigate} history={history} location={location} match={{ params }} />;
  }
  WithRouter.displayName = `withRouter(${Component.displayName || Component.name || "Component"})`;
  return WithRouter;
}
