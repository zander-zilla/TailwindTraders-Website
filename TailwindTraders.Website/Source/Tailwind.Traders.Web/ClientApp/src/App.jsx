import React, { Component } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { connect } from "react-redux";
import { CartService } from "./services";

import { Header, Footer, DebugHeader } from "./shared";
import withRouter from "./shared/withRouter";
import SearchResults from "./pages/search/searchResults";
import {
  Home,
  List,
  MyCoupons,
  Detail,
  SuggestedProductsList,
  Profile,
  ShoppingCart,
} from "./pages";

import "./i18n";
import "./main.scss";

const RoutedList = withRouter(List);
const RoutedDetail = withRouter(Detail);
const RoutedSuggested = withRouter(SuggestedProductsList);
const RoutedHome = withRouter(Home);
const RoutedCoupons = withRouter(MyCoupons);
const RoutedProfile = withRouter(Profile);
const RoutedCart = withRouter(ShoppingCart);

class App extends Component {
  constructor() {
    super();
    this.state = {
      shoppingCart: [],
      quantity: null,
    };
  }

  async componentDidMount() {
    if (this.props.userInfo.token) {
      const shoppingCart = await CartService.getShoppingCart(this.props.userInfo.token);
      if (shoppingCart) {
        this.setState({ shoppingCart });
      }
    }

    if (this.state.shoppingCart != null) {
      const quantity = this.state.shoppingCart.reduce((oldQty, { qty }) => oldQty + qty, 0);
      this.setState({ quantity });
    }
  }

  ShoppingCart = (quantity) => {
    this.setState({ quantity });
  };

  sumProductInState = () => {
    this.setState((prevState) => ({ quantity: prevState.quantity + 1 }));
  };

  render() {
    const { quantity } = this.state;
    const loggedIn = this.props.userInfo.loggedIn === true;
    const privateRoute = (element) => (loggedIn ? element : <Navigate to="/" replace />);

    return (
      <div className="App">
        <BrowserRouter>
          <DebugHeader />
          <Header quantity={quantity} />
          <Routes>
            <Route path="/" element={<RoutedHome />} />
            <Route path="/search" element={<SearchResults />} />
            <Route path="/list" element={<RoutedList />} />
            <Route path="/list/:code" element={<RoutedList />} />
            <Route path="/suggested-products-list/*" element={<RoutedSuggested />} />
            <Route path="/product/detail/:productId/*" element={<RoutedDetail sumProductInState={this.sumProductInState} />} />
            <Route path="/coupons/*" element={privateRoute(<RoutedCoupons />)} />
            <Route path="/profile/*" element={privateRoute(<RoutedProfile />)} />
            <Route
              path="/shopping-cart/*"
              element={privateRoute(<RoutedCart ShoppingCart={this.ShoppingCart} quantity={this.state.quantity} />)}
            />
          </Routes>
          <Footer />
        </BrowserRouter>
      </div>
    );
  }
}

const mapStateToProps = (state) => state.login;

export default connect(mapStateToProps)(App);
