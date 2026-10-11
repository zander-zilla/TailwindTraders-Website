import React, { Component } from 'react';

import { Translation } from 'react-i18next';

import toast from '../../../helpers/toast';

import { UserService } from '../../../services';
import { saveState } from '../../../helpers/localStorage';

import LoginForm from './loginForm';

import Logo from '../../../assets/images/logo-horizontal.svg?react';
import Close from '../../../assets/images/icon-close.svg?react';

class LoginComponent extends Component {
    constructor() {
        super();
        this.state = {
            isModalOpened: false,
            email: "",
            password: "",
            grant_type: "password",
        };
    }

    toggleModalClass = () => {
        this.props.onClickClose();
    }

    keepInputEmail = (e) => {
        const email = this.props.textAction(e.target.value);
        this.setState({ email })
    }

    keepInputPassword = (e) => {
        this.setState({ password: e.target.value })
    }

    handleSubmit = async () => {
        const formData = {
            username: this.state.email.email,
            password: this.state.password,
            grant_type: this.state.grant_type
        }

        if (!this.state.email || !this.state.password) {
            this.handleFormErrors();
            return;
        }

        const loginFormData = await UserService.postLoginForm(formData);

        const LocalStorageInformation = this.generateLocalStorageInformation(loginFormData);

        this.saveDataToLocalStorage(LocalStorageInformation);
        this.props.submitAction(LocalStorageInformation);

        this.toggleModalClass();
    }

    generateLocalStorageInformation(loginFormData) {
        return {
            user: this.state.email,
            token: loginFormData.data.access_token.token,
            refreshToken: loginFormData.data.refresh_token,
            loggedIn: true
        }
    }

    saveDataToLocalStorage(LocalStorageInformation) {
        saveState(LocalStorageInformation);
    }

    handleFormErrors() {
        toast.error("Username or Password can not be empty");
    }

    render() {
        return (
            <Translation>
                {t => (
                    <div className={this.state.isModalOpened ? 'modal-overlay is-opened' : 'modal-overlay'}>
                        <div className="modal">
                            <Close onClick={this.toggleModalClass} />
                            <Logo />
                            <LoginForm
                                    handleSubmit={this.handleSubmit}
                                    keepInputEmail={this.keepInputEmail}
                                    keepInputPassword={this.keepInputPassword}
                                />
                        </div>
                    </div>
                )}
            </Translation>
        );
    }
}

export default LoginComponent;
