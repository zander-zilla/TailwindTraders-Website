import React from "react";
import { Link } from "react-router-dom";
import { withTranslation } from "react-i18next";

import { UploadFile } from "../../../../shared";
import CouponsImg from "../../../../assets/images/icon-coupons.svg?react";

const Banner = ({ t, loggedIn }) => {
    return (
        <div className="banner">
            <div className="banner__buttons">
                <UploadFile title={t("shared.banner.uploadPhotoTitle")} />
                {loggedIn && <Link className="btn btn--secondary" to="/coupons">
                    <CouponsImg />
                    <span>{t("shared.seeMyCoupons")}</span>
                </Link>}
            </div>
        </div>
    );
};

export default withTranslation()(Banner);
